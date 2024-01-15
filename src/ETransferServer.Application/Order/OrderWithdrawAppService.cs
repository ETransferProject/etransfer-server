using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Indexing.Elasticsearch;
using AElf.Types;
using ETransfer.Contracts.TokenPool;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Withdraw.Dtos;
using ETransferServer.WithdrawOrder.Dtos;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Caching;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
using ErrorCode = ETransferServer.Common.ErrorCode;
using NetworkOptions = ETransferServer.Options.NetworkOptions;

namespace ETransferServer.Order;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class OrderWithdrawAppService : ApplicationService, IOrderWithdrawAppService
{
    private const string TokenPoolContractName = "ETransfer.Contracts.TokenPool";
    private const string CaContractName = "Portkey.Contracts.CA";
    private const string CaContractName2 = "Portkey.Contracts.CA2";
    private const string ManagerForwardCall = "ManagerForwardCall";
    private const string TransferToken = "TransferToken";
    private const int ThirdPartDecimals = 6;
    private const int ElfDecimals = 8;

    private readonly INESTRepository<Orders.WithdrawOrder, Guid> _withdrawOrderIndexRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderWithdrawAppService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly INetworkAppService _networkAppService;
    private readonly IContractProvider _contractProvider;
    private readonly WithdrawInfoOptions _withdrawInfoOptions;
    private readonly IOptionsMonitor<NetworkOptions> _networkInfoOptions;
    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly IOptionsMonitor<WithdrawOptions> _withdrawOptions;
    private readonly IDistributedCache<CoBoCoinDto> _coBoCoinCache;
    private readonly IDistributedCache<Tuple<decimal, long>> _minThirdPartFeeCache;


    public OrderWithdrawAppService(INESTRepository<Orders.WithdrawOrder, Guid> WithdrawOrderIndexRepository,
        IObjectMapper objectMapper,
        ILogger<OrderWithdrawAppService> logger, 
        IOptionsMonitor<NetworkOptions> networkInfoOptions,
        IClusterClient clusterClient, 
        INetworkAppService networkAppService, 
        IContractProvider contractProvider,
        IOptionsSnapshot<WithdrawInfoOptions> withdrawInfoOptions,
        IOptionsMonitor<ChainOptions> chainOptions, 
        IOptionsMonitor<WithdrawOptions> withdrawOptions,
        IDistributedCache<CoBoCoinDto> coBoCoinCache, 
        IDistributedCache<Tuple<decimal, long>> minThirdPartFeeCache
        )
    {
        _withdrawOrderIndexRepository = WithdrawOrderIndexRepository;
        _objectMapper = objectMapper;
        _logger = logger;
        _networkInfoOptions = networkInfoOptions;
        _clusterClient = clusterClient;
        _networkAppService = networkAppService;
        _contractProvider = contractProvider;
        _withdrawInfoOptions = withdrawInfoOptions.Value;
        _chainOptions = chainOptions;
        _withdrawOptions = withdrawOptions;
        _coBoCoinCache = coBoCoinCache;
        _minThirdPartFeeCache = minThirdPartFeeCache;
    }

    public async Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request)
    {
        try
        {
            var userId = CurrentUser.GetId();
            AssertHelper.IsTrue(userId != Guid.Empty, "User not exists. Please refresh and try again.");
            AssertHelper.IsTrue(request.ChainId == ChainId.AELF || request.ChainId == ChainId.tDVV
                                                                || request.ChainId == ChainId.tDVW,
                "Param is invalid. Please refresh and try again.");
            AssertHelper.IsTrue(_networkInfoOptions.CurrentValue.NetworkMap.ContainsKey(request.Symbol),
                "Symbol is not exist. Please refresh and try again.");

            var tokenInfoGrain =
                _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(
                    ITokenWithdrawLimitGrain.GenerateGrainId(request.Symbol));

            var tokenLimit = await tokenInfoGrain.GetLimit();
            var withdrawInfoDto = new WithdrawInfoDto();
            withdrawInfoDto.MaxAmount = tokenLimit.RemainingLimit.ToString();
            withdrawInfoDto.LimitCurrency = request.Symbol;
            withdrawInfoDto.RemainingLimit = tokenLimit.RemainingLimit.ToString();
            withdrawInfoDto.TransactionUnit = request.Symbol;
            withdrawInfoDto.TotalLimit =
                _networkInfoOptions.CurrentValue.WithdrawLimit24H.ToString(CultureInfo.InvariantCulture);

            // query async
            var networkFeeTask = CalculateNetworkFeeAsync(request.ChainId);
            var thirdPartFeeTask = request.Network.IsNullOrEmpty()
                ? CalculateThirdPartFeesWithCacheAsync(userId, request.Symbol)
                : CalculateThirdPartFeeAsync(userId, request.Network, request.Symbol);

            var (feeAmount, expireAt) = await thirdPartFeeTask;
            withdrawInfoDto.TransactionFee =
                feeAmount.ToString(ThirdPartDecimals, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.TransactionUnit = request.Symbol;
            withdrawInfoDto.ExpiredTimestamp = expireAt.ToString();

            var networkFee = await networkFeeTask;
            withdrawInfoDto.AelfTransactionFee = networkFee.ToString(ElfDecimals, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.AelfTransactionUnit = CommonConstant.Symbol.Elf;

            var receiveAmount = Math.Max(0, request.Amount) - feeAmount;
            withdrawInfoDto.MinAmount = Math.Max(feeAmount, _withdrawOptions.CurrentValue.MinThirdPartFee)
                .ToString(2, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.ReceiveAmount = receiveAmount > 0
                ? receiveAmount.ToString(ThirdPartDecimals, DecimalHelper.RoundingOption.Ceiling)
                : withdrawInfoDto.ReceiveAmount;
            if (request.Address.IsNullOrEmpty())
                return new GetWithdrawInfoDto { WithdrawInfo = withdrawInfoDto };

            AssertHelper.IsTrue(await IsAddressSupport(request.ChainId, request.Symbol, request.Address),
                "Invalid address. Please refresh and try again.");
            return new GetWithdrawInfoDto
            {
                WithdrawInfo = withdrawInfoDto
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Create withdraw order error, chainId:{chainId}, network:{network}, address:{address}, amount:{amount}, symbol:{symbol}",
                request.ChainId, request.Network, request.Address, request.Amount, request.Symbol);

            throw;
        }
    }


    private async Task<Tuple<decimal, long>> CalculateThirdPartFeesWithCacheAsync(Guid userId, string symbol)
    {
        var thirdFeeCacheKey = CacheKey("minThirdPartFee", symbol);
        var cachedData = await _minThirdPartFeeCache.GetAsync(thirdFeeCacheKey);
        if (cachedData != null) return cachedData;

        var feeData = await CalculateThirdPartFeesAsync(userId, symbol);
        return _minThirdPartFeeCache.GetOrAdd(thirdFeeCacheKey, () => feeData,
            () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.FromUnixTimeMilliseconds(feeData.Item2)
            });
    }

    private async Task<Tuple<decimal, long>> CalculateThirdPartFeesAsync(Guid userId, string symbol)
    {
        Dictionary<string, Task<Tuple<decimal, long>>> fees = new();
        foreach (var network in _networkInfoOptions.CurrentValue.WithdrawFeeNetwork)
        {
            fees.Add(network, CalculateThirdPartFeeAsync(userId, network, symbol));
        }

        decimal minFee = -1;
        long expireAt = -1;
        foreach (var (network, feeTask) in fees)
        {
            try
            {
                var fee = await feeTask;
                minFee = minFee < 0 ? fee.Item1 : Math.Min(fee.Item1, minFee);
                expireAt = fee.Item2;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Query fee error, network={Network} symbol={Symbol}", network, symbol);
            }
        }

        return Tuple.Create(
            minFee < 0 ? _withdrawOptions.CurrentValue.MinThirdPartFee : minFee,
            minFee < 0
                ? DateTime.Now.ToUtcMilliSeconds() + _withdrawOptions.CurrentValue.ThirdPartFeeExpireSeconds * 1000
                : expireAt
        );
    }

    // Estimate transaction fee
    // feeAmount in symbol => expire at milliseconds timestamp
    private async Task<Tuple<decimal, long>> CalculateThirdPartFeeAsync(Guid userId, string network, string symbol)
    {
        var coBoCoinGrain = _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id( network, symbol));
        var coin = await coBoCoinGrain.GetAsync();
        AssertHelper.NotNull(coin, "CoBo coin detail not found");
        _logger.LogDebug("CoBo AbsEstimateFee={Fee}, FeeCoin={Coin}, expireTime={Ts}", coin.AbsEstimateFee,
            coin.FeeCoin, coin.ExpireTime);
        var feeCoin = coin.FeeCoin.Split(CommonConstant.Underline);
        var feeSymbol = feeCoin.Length == 1 ? feeCoin[0] : feeCoin[1];

        var exchangeSymbolPair = string.Join(CommonConstant.Underline, feeSymbol, symbol);
        var exchangeGrain = _clusterClient.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
        var exchange = await exchangeGrain.GetAsync();
        AssertHelper.NotEmpty(exchange, "Exchange data not found {}", exchangeSymbolPair);

        var avgExchange = exchange.Values
            .Where(ex => ex.Exchange > 0)
            .Average(ex => ex.Exchange);
        AssertHelper.IsTrue(avgExchange > 0, "Exchange amount error {}" + avgExchange);
        _logger.LogDebug("Exchange: {Exchange}", string.Join(CommonConstant.Comma,
            exchange.Select(kv => string.Join(CommonConstant.Hyphen, kv.Key, kv.Value.FromSymbol, kv.Value.ToSymbol,
                kv.Value.Exchange, kv.Value.Timestamp)).ToArray()));

        var estimateFee = coin.AbsEstimateFee.SafeToDecimal() * avgExchange;

        var coinFeeCacheKey = CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), network, symbol);
        await _coBoCoinCache.SetAsync(coinFeeCacheKey, new CoBoCoinDto { AbsEstimateFee = estimateFee.ToString(ThirdPartDecimals, DecimalHelper.RoundingOption.Ceiling) }, 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddSeconds(_withdrawInfoOptions.ThirdPartCacheFeeExpireSeconds)
            });
        _logger.LogDebug("Cobo fee set cache: {fee}, {expireSeconds}", estimateFee, _withdrawInfoOptions.ThirdPartCacheFeeExpireSeconds);
            
        var monitorCacheKey = CacheKey(FeeInfo.FeeName.CoBoFee, network);
        if (null == await _coBoCoinCache.GetAsync(monitorCacheKey))
        {
            // If new data is generated
            // go through the monitoring logic.
            await DoMonitorAsync(network, estimateFee, symbol);
            _coBoCoinCache.GetOrAdd(monitorCacheKey, () => coin, () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.FromUnixTimeMilliseconds(coin.ExpireTime)
            });
        }

        return Tuple.Create(estimateFee, coin.ExpireTime);
    }

    public async Task DoMonitorAsync(string network, decimal estimateFee, string symbol)
    {
        await _clusterClient
            .GetGrain<IWithdrawFeeMonitorGrain>(IWithdrawFeeMonitorGrain.GrainId(ThirdPartServiceNameEnum.Cobo,
                network, symbol))
            .DoMonitor(new FeeInfo
            {
                Symbol = symbol,
                Amount = estimateFee.ToString(CultureInfo.InvariantCulture)
            });
    }


    public async Task<decimal> CalculateNetworkFeeAsync(string chainId)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (userId == null || userId == Guid.Empty) return 0;

        var userGrain = _clusterClient.GetGrain<IUserGrain>((Guid)userId);
        var userDto = await userGrain.GetUser();
        AssertHelper.IsTrue(userDto.Success, "User not exists");

        var addressInfo = userDto.Data.AddressInfos.FirstOrDefault(addr => addr.ChainId == chainId);
        AssertHelper.NotEmpty(addressInfo?.Address, "User address of chainId {} empty", chainId);

        var address = Address.FromBase58(addressInfo.Address);
        var balance = await _contractProvider.CallTransactionAsync<GetBalanceOutput>(chainId,
            SystemContractName.TokenContract,
            "GetBalance",
            new GetBalanceInput
            {
                Owner = address,
                Symbol = CommonConstant.Symbol.Elf
            });

        // When the user does not have a balance, the user's money will not be deducted in any case,
        // and when the free amount is not enough, the transaction fee will be deducted from the delegate account.
        if (balance.Balance == 0) return 0;

        if (!_chainOptions.CurrentValue.ChainInfos.TryGetValue(chainId, out var chainInfo))
        {
            return 0;
        }

        var freeAllowances = await _contractProvider.CallTransactionAsync<TransactionFeeFreeAllowancesMap>(chainId,
            SystemContractName.TokenContract, "GetTransactionFeeFreeAllowances", address);
        var totalAllowance = freeAllowances.Map.Values
            .SelectMany(d => d.Map.Values)
            .Sum(m => m.Amount);

        var tokenGrain =
            _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(CommonConstant.Symbol.Elf, chainId));
        var token = await tokenGrain.GetToken();

        var decimalPow = (decimal)Math.Pow(10, token.Decimals);
        var balanceDecimal = balance.Balance / decimalPow;
        var allowanceDecimal = totalAllowance / decimalPow;
        var transactionFee = Math.Max(0, chainInfo.TransactionFee - allowanceDecimal);
        _logger.LogDebug("Fee of address {Address}, balance={Balance}, freeAllowance={Free}, transactionFee={TxFee}",
            addressInfo.Address, balanceDecimal, allowanceDecimal, transactionFee);
        return Math.Min(balanceDecimal, transactionFee);
    }

    private async Task<bool> IsAddressSupport(string chainId, string symbol, string address)
    {
        try
        {
            var network = await _networkAppService.GetNetworkListAsync(new GetNetworkListRequestDto
            {
                Type = OrderTypeEnum.Withdraw.ToString(),
                ChainId = chainId,
                Symbol = symbol,
                Address = address
            });
            return network != null && !network.NetworkList.IsNullOrEmpty();
        }
        catch (UserFriendlyException e)
        {
            return false;
        }
    }

    public async Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(GetWithdrawOrderRequestDto request)
    {
        try {
            var userId = CurrentUser.GetId();
            if (request.FromChainId != ChainId.AELF && request.FromChainId != ChainId.tDVV &&
                request.FromChainId != ChainId.tDVW)
            {
                throw new UserFriendlyException("Param is invalid. Please refresh and try again.");
            }

            AssertHelper.IsTrue(_networkInfoOptions.CurrentValue.NetworkMap.ContainsKey(request.Symbol),
                "Symbol is not exist. Please refresh and try again.");
            AssertHelper.IsTrue(await IsAddressSupport(request.FromChainId, request.Symbol, request.ToAddress),
                "Address invalid");

            var networkConfig = _networkInfoOptions.CurrentValue.NetworkMap[request.Symbol]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.Network);
            AssertHelper.NotNull(networkConfig, "Network is not exist. Please refresh and try again.");

            var userGrain = _clusterClient.GetGrain<IUserGrain>(userId);
            var userDto = await userGrain.GetUser();
            AssertHelper.IsTrue(userDto.Success, "User not exists. Please refresh and try again.");

            var coBoCoinCacheKey = CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), request.Network, request.Symbol);
            var thirdPartFeeDto = await _coBoCoinCache.GetAsync(coBoCoinCacheKey);
            AssertHelper.IsTrue(thirdPartFeeDto != null, ErrorCode.FeeExpired, "Your transaction has expired. Please initiate a new transaction to proceed.");
            _logger.LogDebug("Cobo fee get cache: {fee}", thirdPartFeeDto.AbsEstimateFee);
        
            var inputThirdPartFee = thirdPartFeeDto.AbsEstimateFee.SafeToDecimal(-1);
            AssertHelper.IsTrue(inputThirdPartFee >= 0, "Invalid thirdPart fee");

            // query fees async
            var thirdPartFee = (await CalculateThirdPartFeeAsync(userId, request.Network, request.Symbol)).Item1;
            AssertHelper.IsTrue(
                Math.Abs(inputThirdPartFee - thirdPartFee) / thirdPartFee <=
                _withdrawOptions.CurrentValue.FeeFluctuationPercent,
                ErrorCode.TransactionFeeFluctuatedSignificantly,
                "Transaction failed due to a sudden rise in transaction fees. Please initiate the transaction again.");

            // withdraw fee to thirdPart
            var withdrawAmount = request.Amount - inputThirdPartFee;
            AssertHelper.IsTrue(withdrawAmount > 0, "Insufficient withdraw amount");

            var minWithdraw = Math.Max(thirdPartFee, _withdrawOptions.CurrentValue.MinThirdPartFee)
                .ToString(2, DecimalHelper.RoundingOption.Ceiling)
                .SafeToDecimal();
            AssertHelper.IsTrue(request.Amount >= minWithdraw, "Insufficient withdraw amount");
            
            // Verify that the transaction information matches the order
            var transaction =
                Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(request.RawTransaction));
            var resp = await VerifyTransactionAsync(request.FromChainId, userDto.Data.CaHash, transaction);
            AssertHelper.IsTrue(resp.Success, resp.Message);
            var transferTokenInput = resp.Value;

            var tokenGrain = _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(transferTokenInput.Symbol,
                request.FromChainId));
            var tokenDto = await tokenGrain.GetToken();
            AssertHelper.NotNull(tokenDto, "Symbol token not found {Symbol}", transferTokenInput.Symbol);
            AssertHelper.IsTrue(transferTokenInput.Symbol == request.Symbol, "Invalid symbol of transferTokenInput");

            var expectedAmount = request.Amount * (decimal)Math.Pow(10, tokenDto.Decimals);
            AssertHelper.IsTrue(transferTokenInput.Amount == expectedAmount, "Invalid amount of transferTokenInput");

            // Do create
            return await DoCreateOrderAsync(request, transaction, withdrawAmount,
                networkConfig.WithdrawInfo.WithdrawFee.ToString());
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(e, "Create withdraw order failed");
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Create withdraw order failed");
            throw;
        }
    }


    private async Task<CreateWithdrawOrderDto> DoCreateOrderAsync(GetWithdrawOrderRequestDto request,
        Transaction transaction, decimal withdrawAmount, string feeStr)
    {
        // Replay attacks
        await AssertTxReplayAttacksAsync(transaction);

        // amount limit
        var tokenInfoGrain =
            _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(ITokenWithdrawLimitGrain.GenerateGrainId(request.Symbol));
        AssertHelper.IsTrue(await tokenInfoGrain.Acquire(request.Amount),
            "Amount RemainingLimit is no enough. Please refresh and try again.");
        try
        {
            var orderId = Guid.NewGuid();
            var grain = _clusterClient.GetGrain<IUserWithdrawGrain>(orderId);
            var withdrawOrderDto = new WithdrawOrderDto()
            {
                UserId = CurrentUser.GetId(),
                RawTransaction = request.RawTransaction,
                OrderType = OrderTypeEnum.Withdraw.ToString(),
                FromTransfer = new TransferInfo
                {
                    ChainId = request.FromChainId,
                    Amount = request.Amount,
                    Symbol = request.Symbol,
                    TxId = transaction.GetHash().ToHex(),
                },
                ToTransfer = new TransferInfo
                {
                    ToAddress = request.ToAddress,
                    Amount = withdrawAmount,
                    Network = request.Network,
                    Symbol = request.Symbol,
                    FeeInfo = new List<FeeInfo>
                    {
                        new(request.Symbol, feeStr)
                    }
                }
            };

            var order = await grain.CreateOrder(withdrawOrderDto);
            var getWithdrawOrderInfoDto = new CreateWithdrawOrderDto()
            {
                OrderId = order.Id.ToString()
            };
            return getWithdrawOrderInfoDto;
        }
        catch (Exception e)
        {
            await tokenInfoGrain.Reverse((long)request.Amount);
            _logger.LogError(e,
                "Create withdraw order error, fromChainId:{FromChainId}, network:{Network}, rawTransaction:{RawTransaction}, toAddress:{ToAddress}, amount:{Amount}, symbol:{Symbol}",
                request.FromChainId, request.Network, request.RawTransaction, request.ToAddress, request.Amount,
                request.Symbol);
            throw;
        }
    }

    private async Task AssertTxReplayAttacksAsync(Transaction transaction)
    {
        var rawTransactionHash = transaction.GetHash().ToHex();
        var transactionGrain = _clusterClient.GetGrain<ITransactionGrain>(rawTransactionHash);
        var saveTransactionResult = await transactionGrain.Create();
        AssertHelper.IsTrue(saveTransactionResult.Success, "Transaction id exits TxId={TxId}", rawTransactionHash);
    }

    public async Task<bool> AddOrUpdateAsync(WithdrawOrderDto dto)
    {
        try
        {
            await _withdrawOrderIndexRepository.AddOrUpdateAsync(
                _objectMapper.Map<WithdrawOrderDto, Orders.WithdrawOrder>(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError("Save withdrawOrderIndex fail: {id},{message}", dto.Id, ex.Message);
            return false;
        }

        return true;
    }

    private string CacheKey(params string[] keys)
    {
        return string.Join(CommonConstant.Underline, keys);
    }

    private async Task<CommonResponseDto<TransferTokenInput>> VerifyTransactionAsync(string chainId, string caHash,
        Transaction transaction)
    {
        try
        {
            AssertHelper.IsTrue(transaction.MethodName == ManagerForwardCall, "invalid method name");

            var caContractAddress1 = await _contractProvider.GetContractAddressAsync(chainId, CaContractName);
            var caContractAddress2 = await _contractProvider.GetContractAddressAsync(chainId, CaContractName2);
            AssertHelper.IsTrue(caContractAddress1 == transaction.To.ToBase58()
                                || caContractAddress2 == transaction.To.ToBase58(), "Invalid caContract address");

            var param = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
            AssertHelper.IsTrue(param.MethodName == TransferToken, "Invalid ManagerForwardCall method {Mtd}",
                param.MethodName);
            AssertHelper.IsTrue(param.ContractAddress != new Address(), "Invalid ManagerForwardCall address");
            AssertHelper.IsTrue(!param.Args.IsNullOrEmpty(), "Invalid ManagerForwardCall param");

            var tokenPoolContractAddress = await _contractProvider.GetContractAddressAsync(chainId, TokenPoolContractName);
            AssertHelper.IsTrue(tokenPoolContractAddress == param.ContractAddress.ToBase58(),
                "Invalid tokenPoolContract address");
            AssertHelper.IsTrue(param.CaHash.ToHex() == caHash, "caHash not match");

            var transferTokenInput = TransferTokenInput.Parser.ParseFrom(param.Args);
            AssertHelper.IsTrue(transferTokenInput.Amount > 0, "Tx Token amount {amount} invalid",
                transferTokenInput.Amount);

            return new CommonResponseDto<TransferTokenInput>(transferTokenInput);
        }
        catch (UserFriendlyException e)
        {
            return new CommonResponseDto<TransferTokenInput>().Error(e, e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Verify transaction vs order error, transaction={TxId}",
                transaction.ToByteArray().ToHex());
            return new CommonResponseDto<TransferTokenInput>().Error(e, e.Message);
        }
    }
}