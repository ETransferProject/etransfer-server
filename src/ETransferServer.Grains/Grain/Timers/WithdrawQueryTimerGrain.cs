using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.User;
using MassTransit;
using Microsoft.IdentityModel.Tokens;
using NBitcoin;
using Volo.Abp.Application.Dtos;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWithdrawQueryTimerGrain : IGrainWithGuidKey
{
    Task<DateTime> GetLastCallbackTime();
    Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol);
    Task<Guid> GetUserIdAsync(string address);
    Task Remove(string transactionId);
}

public class WithdrawQueryTimerGrain : Grain<WithdrawTimerOrderState>, IWithdrawQueryTimerGrain
{
    private const int PageSize = 50;
    private const decimal DefaultMinThirdPartFee = 0.1M;
    private DateTime? _lastCallbackTime;

    private readonly ILogger<WithdrawQueryTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOption;
    private readonly IUserAppService _userAppService;

    private readonly ITokenTransferProvider _tokenTransferProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IBus _bus;

    public WithdrawQueryTimerGrain(ILogger<WithdrawQueryTimerGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOption,
        IUserAppService userAppService,
        ITokenTransferProvider tokenTransferProvider,
        IObjectMapper objectMapper, 
        IBus bus)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _withdrawOption = withdrawOption;
        _userAppService = userAppService;
        _tokenTransferProvider = tokenTransferProvider;
        _objectMapper = objectMapper;
        _bus = bus;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("WithdrawQueryTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync(cancellationToken);

        if (State.ExistOrders == null)
        {
            State.ExistOrders = new List<string>();
        }

        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.WithdrawQueryTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawQueryTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("WithdrawQueryTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _lastCallbackTime = DateTime.UtcNow;
        _logger.LogInformation("WithdrawQueryTimerGrain callback, {Time}", DateTime.UtcNow.ToUtc8String());

        var offset = 0;
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var maxTime = State.LastTime;
        _logger.LogInformation("WithdrawQueryTimerGrain lastTime: {LastTime},{GrainId},{Key},{Count}", 
            maxTime, this.GetGrainId(), this.GetPrimaryKey(), State.ExistOrders.Count);
        
        var grain2 = GrainFactory.GetGrain<IUserDepositRecordGrain>(Guid.Parse("93449376-70ae-543f-45f2-baf34c19f297"));
        var deposit = await grain2.GetAsync();

        var grain3 = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(Guid.Parse("89807a6f-708a-4a24-9400-9a6c8c0d94fd"));
        var withdraw = await grain3.Get();
        
        _logger.LogInformation("WithdrawQueryTimerGrain order: {DepositOrderId},{WithdrawOrderId}", 
            deposit?.Value?.Id.ToString(), withdraw?.Value?.Id.ToString());
        
        // while (true)
        // {
        //     var list = new PagedResultDto<TransferRecordDto>();
        //     try
        //     {
        //         list = await _tokenTransferProvider.GetTokenPoolRecordListAsync(State.LastTime,
        //             now, PageSize, offset);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError(e, "get withdraw records error.");
        //     }
        //
        //     if (list.Items.IsNullOrEmpty()) break;
        //     offset += list.Items.Count;
        //     maxTime = list.Items.Max(t => t.Timestamp);
        //     foreach (var transferRecord in list.Items)
        //     {
        //         if (State.ExistOrders.Contains(transferRecord.TransactionId))
        //         {
        //             _logger.LogInformation("order already handle: {id}", transferRecord.TransactionId);
        //             continue;
        //         }
        //
        //         await AddAfter(transferRecord);
        //         _logger.LogInformation("create withdraw order, orderInfo:{orderInfo}",
        //             JsonConvert.SerializeObject(transferRecord));
        //         await CreateWithdrawOrder(transferRecord);
        //     }
        //
        //     if (list.Items.Count < PageSize) break;
        // }
        //
        if (maxTime == 0) maxTime = 1729333800000L;
        State.LastTime = maxTime;
        await WriteStateAsync();
    }

    private async Task CreateWithdrawOrder(TransferRecordDto transferRecord)
    {
        try
        {
            var orderDto = await ConvertToWithdrawOrderDto(transferRecord);

            // Query existing orders first to prevent repeated additions.
            var userWithdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderDto.Id);
            var orderExists = await userWithdrawRecordGrain.Get();
            AssertHelper.IsTrue(orderExists.Success, "Query withdraw exists order failed {Msg}", orderExists.Message);
            AssertHelper.IsNull(orderExists.Value, "Withdraw order {OrderId} exists", orderDto.Id);
            if (orderExists.Value == null)
            {
                await WithdrawLargeAmountAlarmAsync(orderDto);
                await _bus.Publish(_objectMapper.Map<WithdrawOrderDto, OrderChangeEto>(orderDto));
            }
            
            // Save the order, UserWithdrawGrain will process the database multi-write and put the order into the stream for processing.
            var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(orderDto.Id);
            await userWithdrawGrain.AddOrUpdateOrder(orderDto);
        }
        catch (Exception e)
        {
            await WithdrawOrderFailAlarmAsync(transferRecord, e.Message);
            _logger.LogError(e,
                "Create withdraw order error, txId={txId}, symbol={symbol}, amount={amount}",
                transferRecord.TransactionId, transferRecord.Symbol, transferRecord.Amount);
        }
    }
    
    public async Task Remove(string transactionId)
    {
        State.ExistOrders.Remove(transactionId);
        await WriteStateAsync();
    }

    private async Task<WithdrawOrderDto> ConvertToWithdrawOrderDto(TransferRecordDto transferRecord)
    {
        // amount limit
        var (amountDecimal, amountUsd, maxEstimateFee) =
            await CalculateAmountUsdAsync(transferRecord.Symbol, transferRecord.ChainId, transferRecord.Amount,
                transferRecord.MaxEstimateFee);
        var tokenInfoGrain =
            GrainFactory.GetGrain<ITokenWithdrawLimitGrain>(
                ITokenWithdrawLimitGrain.GenerateGrainId(transferRecord.Symbol));
        AssertHelper.IsTrue(await tokenInfoGrain.Acquire(amountUsd), ErrorResult.WithdrawLimitInsufficientCode, null,
            (await tokenInfoGrain.GetLimit()).RemainingLimit, TimeHelper.GetHourDiff(DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1).Date));
        try
        {
            var (withdrawAmount, realFee, isGo) = await RetryCalculateFeeAsync(transferRecord.ToChainId,
                transferRecord.Symbol, amountDecimal, maxEstimateFee);
            AssertHelper.IsTrue(isGo,
                "Invalid amount/fee, amount:{amount}, maxEstimateFee:{maxEstimateFee}, realFee:{realFee}, minWithdraw:{minWithdraw}",
                amountDecimal, maxEstimateFee, realFee, _withdrawOption.Value.MinWithdraw);
            AssertHelper.IsTrue(VerifyByWhiteList(transferRecord.From, transferRecord.ToChainId),
                "The whitelist is not allowed");

            var withdrawOrderDto = new WithdrawOrderDto
            {
                Id = OrderIdHelper.WithdrawOrderId(transferRecord.Id, transferRecord.ToChainId,
                    transferRecord.ToAddress),
                Status = OrderStatusEnum.FromTransferring.ToString(),
                UserId = await GetUserIdAsync(transferRecord.From),
                OrderType = OrderTypeEnum.Withdraw.ToString(),
                AmountUsd = amountUsd,
                FromTransfer = new TransferInfo
                {
                    Network = CommonConstant.Network.AElf,
                    ChainId = transferRecord.ChainId,
                    FromAddress = transferRecord.From,
                    ToAddress = transferRecord.To,
                    Amount = amountDecimal,
                    Symbol = transferRecord.Symbol,
                    TxId = transferRecord.TransactionId,
                    TxTime = DateTime.UtcNow.ToUtcMilliSeconds(),
                    Status = OrderTransferStatusEnum.Transferring.ToString()
                },
                ToTransfer = new TransferInfo
                {
                    Network = VerifyAElfChain(transferRecord.ToChainId)
                        ? CommonConstant.Network.AElf
                        : transferRecord.ToChainId,
                    ChainId = VerifyAElfChain(transferRecord.ToChainId) ? transferRecord.ToChainId : string.Empty,
                    ToAddress = transferRecord.ToAddress,
                    Amount = withdrawAmount,
                    Symbol = transferRecord.Symbol,
                    FeeInfo = new List<FeeInfo>
                    {
                        new(transferRecord.Symbol, realFee.ToString())
                    }
                }
            };
            withdrawOrderDto.ExtensionInfo ??= new Dictionary<string, string>();
            var coBoCoinGrain =
                GrainFactory.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(withdrawOrderDto.ToTransfer.Network,
                    transferRecord.Symbol));
            withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold,
                (await coBoCoinGrain.GetHomogeneousConfirmingThreshold(amountDecimal)).ToString());
            if (!string.IsNullOrWhiteSpace(transferRecord.Memo))
            {
                withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.Memo, transferRecord.Memo);
            }

            return withdrawOrderDto;
        }
        catch (Exception e)
        {
            await tokenInfoGrain.Reverse(amountUsd);
            _logger.LogError(e,
                "WithdrawQueryTimer create withdraw order error, fromChainId:{FromChainId}, " +
                "toChainId:{toChainId}, toAddress:{ToAddress}, amount:{Amount}, symbol:{Symbol}",
                transferRecord.ChainId, transferRecord.ToChainId, transferRecord.ToAddress,
                transferRecord.Amount, transferRecord.Symbol);
            throw;
        }
    }
    
    private async Task WithdrawLargeAmountAlarmAsync(WithdrawOrderDto orderDto)
    {
        var withdrawOrderMonitorGrain = GrainFactory.GetGrain<IWithdrawOrderMonitorGrain>(orderDto.Id.ToString());
        await withdrawOrderMonitorGrain.DoLargeAmountMonitor(orderDto);
    }

    private async Task<Tuple<decimal, decimal, decimal>> CalculateAmountUsdAsync(string symbol, string chainId, long amount, long estimateFee)
    {
        var (amountDecimal, amountUsd, maxEstimateFee) = (0M, 0M, 0M);
        try
        {
            var avgExchange = await GetAvgExchangeAsync(symbol, CommonConstant.Symbol.USD);
            var tokenGrain =
                GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(symbol, chainId));
            var token = await tokenGrain.GetToken();
            AssertHelper.NotNull(token, "Token {symbol}-{chainId} not found", symbol, chainId);

            var decimalPow = (decimal)Math.Pow(10, token.Decimals);
            maxEstimateFee = estimateFee / decimalPow;
            amountDecimal = amount / decimalPow;
            amountUsd = amountDecimal * avgExchange;
            _logger.LogDebug("CalculateAmountUsd: {symbol}, {amount}, {amountUsd}",
                symbol, amount, amountUsd);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CalculateAmountUsd error, symbol: {symbol}", symbol);
        }

        AssertHelper.IsTrue(amountUsd > 0, ErrorResult.TransactionFailCode);
        return Tuple.Create(amountDecimal, amountUsd, maxEstimateFee);
    }

    public async Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol)
    {
        var exchangeSymbolPair = string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
        var exchangeGrain = GrainFactory.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
        var exchange = await exchangeGrain.GetAsync();
        AssertHelper.NotEmpty(exchange, "Exchange data not found {}", exchangeSymbolPair);

        var avgExchange = exchange.Values
            .Where(ex => ex.Exchange > 0)
            .Average(ex => ex.Exchange);
        AssertHelper.IsTrue(avgExchange > 0, "Exchange amount error {}", avgExchange);
        _logger.LogDebug("Exchange: {Exchange}", string.Join(CommonConstant.Comma,
            exchange.Select(kv => string.Join(CommonConstant.Hyphen, kv.Key,
                kv.Value.FromSymbol, kv.Value.ToSymbol, kv.Value.Exchange, kv.Value.Timestamp)).ToArray()));
        return avgExchange;
    }

    public async Task<Guid> GetUserIdAsync(string address)
    {
        if (address.IsNullOrEmpty()) return Guid.Empty;
        var user = await _userAppService.GetUserByAddressAsync(address);
        return user?.Id ?? OrderIdHelper.WithdrawUserId(address);
    }
    
    private bool VerifyAElfChain(string chainId)
    {
        return chainId == ChainId.AELF || chainId == ChainId.tDVV || chainId == ChainId.tDVW;
    }

    private async Task<decimal> CalculateNetworkFeeAsync(string network, string symbol)
    {
        if (VerifyAElfChain(network))
        {
            return _withdrawOption.Value.Homogeneous.ContainsKey(symbol)
                ? _withdrawOption.Value.Homogeneous[symbol].WithdrawFee
                : 0M;
        }

        var coBoCoinGrain = GrainFactory.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(network, symbol));
        var coin = await coBoCoinGrain.Get();
        AssertHelper.NotNull(coin, "CoBo coin detail not found");
        _logger.LogDebug("CoBo AbsEstimateFee={Fee}, FeeCoin={Coin}, expireTime={Ts}", coin.AbsEstimateFee,
            coin.FeeCoin, coin.ExpireTime);
        var feeCoin = coin.FeeCoin.Split(CommonConstant.Underline);
        var feeSymbol = feeCoin.Length == 1 ? feeCoin[0] : feeCoin[1];

        var avgExchange = await GetAvgExchangeAsync(feeSymbol, symbol);
        var estimateFee = coin.AbsEstimateFee.SafeToDecimal() * avgExchange;
        estimateFee = Math.Max(estimateFee, await GetMinThirdPartFeeAsync(network, symbol))
            .ToString(GetDecimals(symbol), DecimalHelper.RoundingOption.Ceiling).SafeToDecimal();
        return estimateFee;
    }

    private int GetDecimals(string symbol)
    {
        return _withdrawOption.Value.TokenInfo.ContainsKey(symbol)
            ? _withdrawOption.Value.TokenInfo[symbol]
            : DecimalHelper.GetDecimals(symbol);
    }

    private decimal AssertWithdrawAmount(decimal amount, decimal estimateFee, decimal realFee)
    {
        if (estimateFee > 0)
        {
            AssertHelper.IsTrue(estimateFee >= realFee, ErrorResult.FeeInvalidCode);
        }

        var withdrawAmount = amount - realFee;
        AssertHelper.IsTrue(withdrawAmount > 0, ErrorResult.AmountInsufficientCode);
        var minWithdraw = Math.Max(realFee, _withdrawOption.Value.MinWithdraw)
            .ToString(2, DecimalHelper.RoundingOption.Ceiling)
            .SafeToDecimal();
        AssertHelper.IsTrue(amount >= minWithdraw, ErrorResult.AmountInsufficientCode);
        return withdrawAmount;
    }

    private async Task<Tuple<decimal, decimal, bool>> RetryCalculateFeeAsync(string chainId, string symbol,
        decimal amount, decimal estimateFee)
    {
        var retry = 0;
        var (withdrawAmount, realFee) = (0M, 0M);
        do
        {
            try
            {
                ++retry;
                _logger.LogInformation("Retry amount:{amount}, maxEstimateFee:{maxEstimateFee}, realFee:{realFee}",
                    amount, estimateFee, realFee);
                realFee = await CalculateNetworkFeeAsync(chainId, symbol);
                withdrawAmount = AssertWithdrawAmount(amount, estimateFee, realFee);
                return Tuple.Create(withdrawAmount, realFee, true);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Compare maxEstimateFee, retry: {retry}", retry);
                await Task.Delay(1000);
            }
        } while (retry <= _withdrawOption.Value.ToTransferMaxRetry);

        return Tuple.Create(withdrawAmount, realFee, false);
    }
    
    public Task<decimal> GetMinThirdPartFeeAsync(string network, string symbol)
    {
        var minFeeKey = ICoBoCoinGrain.Id(network, symbol);
        return Task.FromResult(_withdrawOption.Value.MinThirdPartFee.ContainsKey(minFeeKey)
            ? _withdrawOption.Value.MinThirdPartFee[minFeeKey]
            : DefaultMinThirdPartFee);
    }
    
    private bool VerifyByWhiteList(string fromAddress, string toChainId)
    {
        _logger.LogInformation("VerifyByWhiteList fromAddress:{fromAddress},toChainId:{toChainId}", fromAddress, toChainId);
        return _withdrawOption.Value.SupportWhiteLists.IsNullOrEmpty()
               || !_withdrawOption.Value.SupportWhiteLists.ContainsKey(toChainId)
               || (_withdrawOption.Value.SupportWhiteLists.ContainsKey(toChainId)
                   && _withdrawOption.Value.SupportWhiteLists[toChainId].Contains(fromAddress));
    }

    private async Task AddAfter(TransferRecordDto transferRecord)
    {
        if (State.ExistOrders.Count > _withdrawOption.Value.MaxListLength)
            State.ExistOrders.RemoveAt(0);
        State.ExistOrders.Add(transferRecord.TransactionId);
    }

    private async Task WithdrawOrderFailAlarmAsync(TransferRecordDto recordDto, string reason)
    {
        var withdrawOrderMonitorGrain = GrainFactory.GetGrain<IWithdrawOrderMonitorGrain>(recordDto.Id);
        await withdrawOrderMonitorGrain.DoMonitor(WithdrawOrderMonitorDto.Create(recordDto, reason));
    }

    public Task<DateTime> GetLastCallbackTime()
    {
        return Task.FromResult(_lastCallbackTime ?? DateTime.MinValue);
    }
}