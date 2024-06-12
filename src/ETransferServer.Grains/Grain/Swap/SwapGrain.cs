using System.Numerics;
using AElf;
using AElf.Client.Dto;
using AElf.Types;
using Awaken.Contracts.Swap;
using ETransfer.Contracts.TokenPool;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Options;
using Orleans;
using ETransferServer.Grains.State.Swap;
using ETransferServer.Options;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using Newtonsoft.Json;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Swap;

public interface ISwapGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<DepositOrderChangeDto>> SwapAsync(DepositOrderDto dto);

    // Task<GrainResultDto<DepositOrderChangeDto>> SubsidyTransferAsync(DepositOrderDto dto，string returnValue);
    Task<decimal> ParseReturnValueAsync(LogEventDto[] logs);
    Task<decimal> RecordAmountOutAsync(long amount);
}

public class SwapGrain : Grain<SwapState>, ISwapGrain
{
    private readonly SwapInfosOptions _swapInfosOptions;
    private readonly IContractProvider _contractProvider;
    private readonly ChainOptions _chainOptions;
    private readonly ILogger<SwapGrain> _logger;
    private readonly IObjectMapper _objectMapper;
    private readonly IBlockchainClientProviderFactory _blockchainClientProvider;

    public SwapGrain(IOptionsSnapshot<SwapInfosOptions> swapInfosOptions, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, ILogger<SwapGrain> logger, IObjectMapper objectMapper,
        IBlockchainClientProviderFactory blockchainClientProvider)
    {
        _contractProvider = contractProvider;
        _logger = logger;
        _objectMapper = objectMapper;
        _blockchainClientProvider = blockchainClientProvider;
        _chainOptions = chainOptions.Value;
        _swapInfosOptions = swapInfosOptions.Value;
    }

    internal JsonSerializerSettings JsonSettings = JsonSettingsBuilder.New()
        .WithAElfTypesConverters()
        .WithCamelCasePropertyNamesResolver()
        .Build();

    public async Task<GrainResultDto<DepositOrderChangeDto>> SwapAsync(DepositOrderDto dto)
    {
        var result = new GrainResultDto<DepositOrderChangeDto>()
        {
            Data = new DepositOrderChangeDto
            {
                DepositOrder = dto
            }
        };

        try
        {
            var toTransfer = dto.ToTransfer;
            var fromTransfer = dto.FromTransfer;
            var pairSymbol = GeneratePairSymbol(fromTransfer.Symbol, toTransfer.Symbol);
            _logger.LogInformation("Start to swap {pairSymbol}", pairSymbol);
            var res = _swapInfosOptions.PairInfos.TryGetValue(pairSymbol, out var swapInfo);
            if (!res)
            {
                _logger.LogError("Failed to get swap info.{grainId}", this.GetPrimaryKey().ToString());
                result.Success = false;
                result.Message = "Failed to get pair info.";
                return result;
            }

            Transaction rawTransaction;
            if (dto.FromRawTransaction.IsNullOrEmpty())
            {
                State.ToChainId = dto.ToTransfer.ChainId;
                State.SymbolIn = dto.FromTransfer.Symbol;
                State.SymbolOut = dto.ToTransfer.Symbol;
                State.AmountIn = dto.FromTransfer.Amount;
                await WriteStateAsync();
                long timestamp = 0;
                _logger.LogInformation("New swap transaction will struct.{grainId}", this.GetPrimaryKey().ToString());
                if (dto.FromTransfer.TxTime == null && State.TimeStamp == null)
                {
                    var timeRes = await GetTransactionTimeAsync(dto.FromTransfer.Network,
                        dto.FromTransfer.BlockHash,
                        fromTransfer.TxId);
                    if (!timeRes.Success)
                    {
                        result.Success = false;
                        result.Message = timeRes.Message;
                        return result;
                    }

                    timestamp = timeRes.Data;
                    State.TimeStamp = timestamp;
                }
                else
                {
                    timestamp = (long)(dto.FromTransfer.TxTime ?? State.TimeStamp);
                }

                _logger.LogInformation("Get transaction time from {network},{time},{grainId}", dto.FromTransfer.Network,
                    timestamp, this.GetPrimaryKey().ToString());
                dto.FromTransfer.TxTime = timestamp;
                var (swapCheckResult, swapInput) =
                    await StructSwapTransactionAsync(dto.Id, fromTransfer, toTransfer, timestamp, swapInfo);
                if (!swapCheckResult.Success)
                {
                    result.Message = swapCheckResult.Message;
                    result.Success = false;
                    return result;
                }

                // create swap transaction
                var (txId, newTransaction) = await _contractProvider.CreateTransactionAsync(toTransfer.ChainId,
                    _chainOptions.ChainInfos[toTransfer.ChainId].ReleaseAccount,
                    CommonConstant.ETransferTokenPoolContractName, CommonConstant.ETransferSwapToken, swapInput);

                toTransfer.TxId = txId.ToHex();
                rawTransaction = newTransaction;
                dto.FromRawTransaction = newTransaction.ToByteArray().ToHex();
            }
            else
            {
                rawTransaction = Transaction.Parser.ParseFrom(ByteStringHelper.FromHexString(dto.FromRawTransaction));
            }

            toTransfer.TxTime = DateTime.UtcNow.ToUtcMilliSeconds();
            toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();

            dto.Status = OrderStatusEnum.ToTransferring.ToString();

            var depositRecordGrain = GrainFactory.GetGrain<IUserDepositGrain>(this.GetPrimaryKey());
            await depositRecordGrain.AddOrUpdateOrder(dto, ExtensionBuilder.New()
                .Add(ExtensionKey.IsForward, Boolean.FalseString)
                .Add(ExtensionKey.TransactionId, toTransfer.TxId)
                .Add(ExtensionKey.Transaction, JsonConvert.SerializeObject(rawTransaction, JsonSettings))
                .Build());

            // send 
            var (isSuccess, error) = await _contractProvider.SendTransactionAsync(toTransfer.ChainId, rawTransaction);
            AssertHelper.IsTrue(isSuccess, error);

            var txResult = await _contractProvider.WaitTransactionResultAsync(toTransfer.ChainId, toTransfer.TxId,
                _chainOptions.Contract.WaitSecondsAfterSend * 1000,
                _chainOptions.Contract.RetryDelaySeconds * 1000);

            switch (txResult.Status)
            {
                case CommonConstant.TransactionState.Mined:
                    toTransfer.Status = OrderTransferStatusEnum.Transferred.ToString();
                    dto.Status = OrderStatusEnum.ToTransferred.ToString();
                    break;
                case CommonConstant.TransactionState.NodeValidationFailed:
                    toTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
                    dto.Status = OrderStatusEnum.ToTransferFailed.ToString();
                    result.Success = false;
                    break;
                default:
                    toTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
                    dto.Status = OrderStatusEnum.ToTransferring.ToString();
                    break;
            }

            result.Data = new DepositOrderChangeDto
            {
                DepositOrder = dto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionStatus, txResult.Status)
                    .Add(ExtensionKey.TransactionError, txResult.Error)
                    .Build()
            };
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Deposit order handle error, status={Status}",
                OrderStatusEnum.ToStartTransfer.ToString());

            dto.ToTransfer.Status = OrderTransferStatusEnum.Failed.ToString();
            dto.Status = OrderStatusEnum.ToTransferFailed.ToString();
            result.Data = new DepositOrderChangeDto
            {
                DepositOrder = dto,
                ExtensionData = ExtensionBuilder.New()
                    .Add(ExtensionKey.TransactionError, e.Message)
                    .Build()
            };
            result.Success = false;
            result.Message = e.Message;
            return result;
        }
    }

    private async Task<GrainResultDto<long>> GetTransactionTimeAsync(string chainId, string blockHash, string txId)
    {
        try
        {
            var grain = GrainFactory.GetGrain<IEvmTransactionGrain>(string.Join(CommonConstant.Underline, chainId,
                blockHash));
            var resultDto = await grain.GetTransactionTimeAsync(chainId, blockHash, txId);
            return resultDto;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get {chainId} transaction time.{grainId}", chainId,
                this.GetPrimaryKeyString());
            return new GrainResultDto<long>
            {
                Success = false,
                Message = "Failed to get transaction time."
            };
        }
    }

    private static string GeneratePairSymbol(params string[] symbols)
    {
        return symbols.JoinAsString("-");
    }

    private async Task<(GrainResultDto, IMessage input)> StructSwapTransactionAsync(Guid orderId,
        TransferInfo fromTransfer,
        TransferInfo toTransfer, long createTime, SwapInfo swapInfo)
    {
        var result = new GrainResultDto();
        decimal amountsExpected = 0;
        // 1. get create time reserve
        var fromAmount = fromTransfer.Amount;
        for (var i = 0; i < swapInfo.Path.Count - 1; i++)
        {
            var reserveResult = await GetOrderTimeReserveAsync(orderId, fromTransfer, swapInfo.Path[i],
                swapInfo.Path[i + 1],
                toTransfer.ChainId, swapInfo.Router,
                createTime);
            if (!reserveResult.Success)
            {
                _logger.LogError("{Message}.{orderId}", reserveResult.Message, this.GetPrimaryKey().ToString());
                result.Success = false;
                result.Message = reserveResult.Message;
                return (result, null);
            }

            var (fromReserve, toReserve) = await DealReserveAsync(swapInfo.Path[i], reserveResult.Data);
            _logger.LogInformation("Success to get reserve.{reserveIn},{reserveOut},{grainId}", fromReserve, toReserve,
                this.GetPrimaryKey());
            // 2. calculate create time amounts out
            var (amountsOutPre, amountsOutPreWithDecimal) =
                await CalculateAmountsOutPreAsync(swapInfo.FeeRate, swapInfo.Path[i], fromAmount, swapInfo.Path[i + 1],
                    toTransfer.ChainId, fromReserve, toReserve);
            fromAmount = amountsOutPre;
            _logger.LogInformation("Amounts out expected {fromSymbol},{toSymbol},{amount},{grainId}", swapInfo.Path[i],
                swapInfo.Path[i + 1], amountsOutPre, this.GetPrimaryKey());
            amountsExpected = amountsOutPre;
        }

        // 3. get now amounts out
        var (amountIn, amountOut, amountInWithDecimal, amountOutWithDecimal) =
            await GetAmountsOutNowAsync(fromTransfer, toTransfer, swapInfo);
        _logger.LogInformation("Amounts out now.{amount},{grainId}", amountOut, this.GetPrimaryKey());

        var swapInput = new SwapTokenInput
        {
            AmountIn = amountInWithDecimal,
            To = Address.FromBase58(toTransfer.ToAddress),
            Deadline = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Channel = this.GetPrimaryKey().ToString().Replace("-", ""),
            Path = { swapInfo.Path },
            FeeRate = (long)(swapInfo.FeeRate * 10000),
            From = Address.FromBase58(toTransfer.FromAddress)
        };

        // 4. get slippage and compare amount and get amount out min
        var (success, amountsOutMin) = await CompareSlippageAndGetAmountsOutMinAsync(amountsExpected, swapInfo.Slippage,
            amountOut, toTransfer.Symbol, toTransfer.ChainId);
        if (success)
        {
            swapInput.AmountOutMin = amountsOutMin;
        }
        else
        {
            result.Success = false;
            result.Message = "Exceed slippage.";
            _logger.LogError("{message},{orderId}", result.Message, this.GetPrimaryKey().ToString());
            return (result, null);
        }

        _logger.LogInformation("Swap input.{input},{grainId}", JsonConvert.SerializeObject(swapInput),
            this.GetPrimaryKey().ToString());

        return (result, swapInput);
    }

    private async Task<(long, long)> DealReserveAsync(string fromSymbol, ReserveDto reserve)
    {
        var isReversed = fromSymbol == reserve.SymbolA;
        long fromReserve;
        long toReserve;
        if (isReversed)
        {
            fromReserve = reserve.ReserveA;
            toReserve = reserve.ReserveB;
        }
        else
        {
            fromReserve = reserve.ReserveB;
            toReserve = reserve.ReserveA;
        }

        State.ReserveIn = fromReserve;
        State.ReserveOut = toReserve;
        await WriteStateAsync();
        return (fromReserve, toReserve);
    }

    private async Task<(decimal, long)> CalculateAmountsOutPreAsync(decimal feeRate, string fromSymbol,
        decimal fromAmount,
        string toSymbol, string chainId, long fromReserve, long toReserve)
    {
        var fromToken = await GetTokenAsync(fromSymbol, chainId);
        var toToken = await GetTokenAsync(toSymbol, chainId);
        var fromTokenActualAmount = (new BigDecimal(fromAmount) * BigInteger.Pow(10, fromToken.Decimals));
        var amountInWithFee = (10000 - feeRate * 10000) * fromTokenActualAmount;
        var numerator = amountInWithFee * new BigInteger(toReserve);
        var denominator = new BigInteger(fromReserve) * 10000 + amountInWithFee;
        var amountsOutPreWithDecimal = (long)Math.Floor((decimal)(numerator / denominator));
        var amountsOutPre = amountsOutPreWithDecimal / (decimal)Math.Pow(10, toToken.Decimals);
        State.AmountOutPre = amountsOutPre;
        await WriteStateAsync();
        return (amountsOutPre, amountsOutPreWithDecimal);
    }

    private async Task<(decimal, decimal, long, long)> GetAmountsOutNowAsync(TransferInfo fromTransfer,
        TransferInfo toTransfer, SwapInfo swapInfo)
    {
        var swapAmountsGrain = GrainFactory.GetGrain<ISwapAmountsOutGrain>(ISwapAmountsOutGrain.GenGrainId(
            toTransfer.ChainId,
            fromTransfer.Symbol, toTransfer.Symbol, swapInfo.Router));
        var (amountIn, amountOut, amountInActual, amountOutActual) =
            await swapAmountsGrain.GetAmountsOutAsync(fromTransfer.Amount, swapInfo.Path);
        State.AmountOutNow = amountOut;
        await WriteStateAsync();
        return (amountIn, amountOut, amountInActual, amountOutActual);
    }

    private async Task<(bool, long)> CompareSlippageAndGetAmountsOutMinAsync(decimal expectedAmountsOut,
        decimal slippage, decimal availableAmountsOut, string symbolOut, string chainId)
    {
        var expectedMinAmountsOut = expectedAmountsOut * (1 - slippage);
        _logger.LogInformation("After calculating slippage, the minimum obtainable amounts:{amount},orderId:{orderId}",
            expectedMinAmountsOut, this.GetPrimaryKey().ToString());
        if (availableAmountsOut < expectedMinAmountsOut)
        {
            _logger.LogInformation(
                "The amount currently available is less than the expected amount.{available},{expected},orderId:{orderId}",
                availableAmountsOut, expectedMinAmountsOut, this.GetPrimaryKey().ToString());
            return (false, 0);
        }

        var token = await GetTokenAsync(symbolOut, chainId);
        var amountsOutMin = (new BigDecimal(expectedMinAmountsOut) * BigInteger.Pow(10, token.Decimals)).Floor();
        AssertHelper.IsTrue(long.TryParse(amountsOutMin.ToString(), out var amountsOutMinActual));
        State.AmountOutMin = amountsOutMinActual;
        await WriteStateAsync();
        return (true, amountsOutMinActual);
    }

    private async Task<GrainResultDto<ReserveDto>> GetOrderTimeReserveAsync(Guid orderId, TransferInfo fromTransfer,
        string fromSymbol,
        string toSymbol, string chainId, string router, long createTime)
    {
        var reserveGrain = GrainFactory.GetGrain<ISwapReserveGrain>(ISwapReserveGrain.GenGrainId(
            chainId,
            fromSymbol, toSymbol, router));
        var reserveResult = await reserveGrain.GetReserveAsync(orderId, createTime, 0, 10);
        return reserveResult;
    }

    private async Task<TokenDto> GetTokenAsync(string symbol, string chainId)
    {
        var tokenGrain =
            GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(symbol, chainId));
        var tokenInfo = await tokenGrain.GetToken();
        AssertHelper.NotNull(tokenInfo, "Token info {symbol}-{chainId} not found", symbol,
            chainId);
        return tokenInfo;
    }

    public async Task<decimal> ParseReturnValueAsync(LogEventDto[] logs)
    {
        decimal actualSwappedAmountOut = 0;
        try
        {
            if (logs.Length > 0)
            {
                var swapLog = logs.FirstOrDefault(l => l.Name == nameof(TokenSwapped))?.NonIndexed;
                if (!swapLog.IsNullOrWhiteSpace())
                {
                    var amountsOut = TokenSwapped.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(swapLog)).AmountOut;
                    _logger.LogInformation("Amounts out parsed:{amount}",amountsOut.AmountOut.Last());
                    var tokenInfo = await GetTokenAsync(State.SymbolOut, State.ToChainId);
                    actualSwappedAmountOut = (amountsOut.AmountOut.Last() / (decimal)Math.Pow(10, tokenInfo.Decimals));
                    State.ActualSwappedAmountOut = actualSwappedAmountOut;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse.");
            return 0;
        }
        return actualSwappedAmountOut;
    }

    public async Task<decimal> RecordAmountOutAsync(long amount)
    {
        decimal actualSwappedAmountOut = 0;
        try
        {
            var tokenInfo = await GetTokenAsync(State.SymbolOut, State.ToChainId);
            actualSwappedAmountOut = (amount / (decimal)Math.Pow(10, tokenInfo.Decimals));
            State.ActualSwappedAmountOut = actualSwappedAmountOut;
            await WriteStateAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse.");
            return 0;
        }

        return actualSwappedAmountOut;
    }
}