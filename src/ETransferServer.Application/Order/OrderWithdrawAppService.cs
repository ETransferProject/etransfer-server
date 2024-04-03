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
using Newtonsoft.Json;
using Orleans;
using Portkey.Contracts.CA;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Caching;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;
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
    private const string PortKeyVersion = "v1";
    private const string PortKeyVersion2 = "v2";
    private const int ElfDecimals = 8;

    private readonly INESTRepository<Orders.OrderIndex, Guid> _withdrawOrderIndexRepository;
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


    public OrderWithdrawAppService(INESTRepository<Orders.OrderIndex, Guid> withdrawOrderIndexRepository,
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
        _withdrawOrderIndexRepository = withdrawOrderIndexRepository;
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
            AssertHelper.IsTrue(
                string.IsNullOrWhiteSpace(request.Version) || PortKeyVersion.Equals(request.Version) ||
                PortKeyVersion2.Equals(request.Version), "Version is invalid. Please refresh and try again.");

            var tokenInfoGrain =
                _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(
                    ITokenWithdrawLimitGrain.GenerateGrainId(request.Symbol));

            var tokenLimit = await tokenInfoGrain.GetLimit();
            var withdrawInfoDto = new WithdrawInfoDto();
            withdrawInfoDto.LimitCurrency = request.Symbol;
            withdrawInfoDto.TransactionUnit = request.Symbol;

            // query async
            var networkFeeTask = CalculateNetworkFeeAsync(request.ChainId, request.Version);
            var decimals = DecimalHelper.GetDecimals(request.Symbol);
            var thirdPartFeeTask = request.Network.IsNullOrEmpty()
                ? CalculateThirdPartFeesWithCacheAsync(userId, request.Symbol)
                : CalculateThirdPartFeeAsync(userId, request.Network, request.Symbol);

            var (feeAmount, expireAt) = await thirdPartFeeTask;
            withdrawInfoDto.TransactionFee = feeAmount.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.TransactionUnit = request.Symbol;
            withdrawInfoDto.ExpiredTimestamp = expireAt.ToString();

            var networkFee = await networkFeeTask;
            withdrawInfoDto.AelfTransactionFee = networkFee.ToString(ElfDecimals, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.AelfTransactionUnit = CommonConstant.Symbol.Elf;

            var receiveAmount = Math.Max(0, request.Amount) - feeAmount;
            withdrawInfoDto.MinAmount = Math.Max(feeAmount, _withdrawOptions.CurrentValue.MinThirdPartFee)
                .ToString(2, DecimalHelper.RoundingOption.Ceiling);
            withdrawInfoDto.ReceiveAmount = receiveAmount > 0
                ? receiveAmount.ToString(decimals, DecimalHelper.RoundingOption.Ceiling)
                : withdrawInfoDto.ReceiveAmount ?? default(int).ToString();
            try
            {
                var avgExchange =
                    await _networkAppService.GetAvgExchangeAsync(request.Symbol, CommonConstant.Symbol.USD);
                withdrawInfoDto.TotalLimit =
                    (_networkInfoOptions.CurrentValue.WithdrawLimit24H / avgExchange).ToString(decimals,
                        DecimalHelper.RoundingOption.Ceiling);
                withdrawInfoDto.MaxAmount = (tokenLimit.RemainingLimit / avgExchange).ToString(decimals,
                    DecimalHelper.RoundingOption.Ceiling);
                withdrawInfoDto.RemainingLimit = withdrawInfoDto.MaxAmount;
                withdrawInfoDto.AmountUsd =
                    (request.Amount * avgExchange).ToString(decimals,
                        DecimalHelper.RoundingOption.Ceiling);
                withdrawInfoDto.ReceiveAmountUsd =
                    (withdrawInfoDto.ReceiveAmount.SafeToDecimal() * avgExchange).ToString(decimals,
                        DecimalHelper.RoundingOption.Ceiling);
                var fee = feeAmount * avgExchange;
                if (networkFee > 0)
                {
                    avgExchange =
                        await _networkAppService.GetAvgExchangeAsync(CommonConstant.Symbol.Elf, CommonConstant.Symbol.USD);
                    fee += networkFee * avgExchange;
                }
                withdrawInfoDto.FeeUsd = fee.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Get withdraw avg exchange failed.");
            }

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
        var (estimateFee, coin) = await _networkAppService.CalculateNetworkFeeAsync(network, symbol);

        var coinFeeCacheKey = CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), network, symbol);
        var decimals = DecimalHelper.GetDecimals(symbol);
        await _coBoCoinCache.SetAsync(coinFeeCacheKey, new CoBoCoinDto { AbsEstimateFee = estimateFee.ToString(decimals, DecimalHelper.RoundingOption.Ceiling) }, 
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
            await DoMonitorAsync(network, coin.AbsEstimateFee.SafeToDecimal(), coin.FeeCoin);
            _coBoCoinCache.GetOrAdd(monitorCacheKey, () => coin, () => new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.FromUnixTimeMilliseconds(coin.ExpireTime)
            });
        }

        return Tuple.Create(estimateFee, coin.ExpireTime);
    }

    public async Task DoMonitorAsync(string network, decimal estimateFee, string symbol)
    {
        _logger.LogDebug("Withdraw fee monitor, network={Network}, fee={Fee}, symbol={Symbol}", network, estimateFee, symbol);
        await _clusterClient
            .GetGrain<IWithdrawFeeMonitorGrain>(IWithdrawFeeMonitorGrain.GrainId(ThirdPartServiceNameEnum.Cobo,
                network, symbol))
            .DoMonitor(new FeeInfo
            {
                Symbol = symbol,
                Amount = estimateFee.ToString(CultureInfo.InvariantCulture)
            });
    }


    private async Task<decimal> CalculateNetworkFeeAsync(string chainId, string version)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (userId == null || userId == Guid.Empty) return 0;

        var userGrain = _clusterClient.GetGrain<IUserGrain>((Guid)userId);
        var userDto = await userGrain.GetUser();
        AssertHelper.IsTrue(userDto.Success, "User not exists");

        var addressInfo = userDto.Data.AddressInfos.FirstOrDefault(addr => addr.ChainId == chainId);
        var address = addressInfo?.Address.IsNullOrEmpty() ?? true
            ? ConvertVirtualAddressToContractAddress(Hash.LoadFromHex(userDto.Data.CaHash),
                Address.FromBase58(await _contractProvider.GetContractAddressAsync(chainId,
                    PortKeyVersion2.Equals(version) ? CaContractName2 : CaContractName)))
            : Address.FromBase58(addressInfo.Address);
        _logger.LogInformation(
            "Get address when calculate fee: {address}, userId: {userId}, chainId: {chainId}, version: {version}",
            address, userId.ToString(), chainId, version);

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

    private Address ConvertVirtualAddressToContractAddress(Hash virtualAddress, Address contractAddress)
    {
        return Address.FromPublicKey(contractAddress.Value.Concat(virtualAddress.Value.ToByteArray().ComputeHash())
            .ToArray());
    }

    private async Task<bool> IsAddressSupport(string chainId, string symbol, string address)
    {
        try
        {
            var network = await _networkAppService.GetNetworkListWithLocalFeeAsync(new GetNetworkListRequestDto
            {
                Type = OrderTypeEnum.Withdraw.ToString(),
                ChainId = chainId,
                Symbol = symbol,
                Address = address
            });
            return network != null && !network.NetworkList.IsNullOrEmpty();
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public async Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(GetWithdrawOrderRequestDto request)
    {
        try
        {
            _logger.LogDebug("CreateWithdrawOrder: {request}", JsonConvert.SerializeObject(request));
            var userId = CurrentUser.GetId();
            AssertHelper.IsTrue(
                request.FromChainId == ChainId.AELF || request.FromChainId == ChainId.tDVV ||
                request.FromChainId == ChainId.tDVW, ErrorResult.ChainIdInvalidCode);
            AssertHelper.IsTrue(_networkInfoOptions.CurrentValue.NetworkMap.ContainsKey(request.Symbol),
                ErrorResult.SymbolInvalidCode, null, request.Symbol);
            AssertHelper.IsTrue(await IsAddressSupport(request.FromChainId, request.Symbol, request.ToAddress),
                ErrorResult.AddressInvalidCode);

            var networkConfig = _networkInfoOptions.CurrentValue.NetworkMap[request.Symbol]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.Network);
            AssertHelper.NotNull(networkConfig, ErrorResult.NetworkInvalidCode);

            var userGrain = _clusterClient.GetGrain<IUserGrain>(userId);
            var userDto = await userGrain.GetUser();
            AssertHelper.IsTrue(userDto.Success, ErrorResult.JwtInvalidCode);

            var coBoCoinCacheKey =
                CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), request.Network, request.Symbol);
            var thirdPartFeeDto = await _coBoCoinCache.GetAsync(coBoCoinCacheKey);
            AssertHelper.IsTrue(thirdPartFeeDto != null, ErrorResult.FeeExpiredCode);
            _logger.LogDebug("Cobo fee get cache: {fee}", thirdPartFeeDto.AbsEstimateFee);

            var inputThirdPartFee = thirdPartFeeDto.AbsEstimateFee.SafeToDecimal(-1);
            AssertHelper.IsTrue(inputThirdPartFee >= 0, ErrorResult.FeeInvalidCode);

            // query fees async
            var thirdPartFee = (await CalculateThirdPartFeeAsync(userId, request.Network, request.Symbol)).Item1;
            AssertHelper.IsTrue(
                Math.Abs(inputThirdPartFee - thirdPartFee) / thirdPartFee <=
                _withdrawOptions.CurrentValue.FeeFluctuationPercent,
                ErrorResult.FeeExceedCode, null, request.Network);

            // withdraw fee to thirdPart
            var withdrawAmount = request.Amount - inputThirdPartFee;
            AssertHelper.IsTrue(withdrawAmount > 0, ErrorResult.AmountInsufficientCode);

            var minWithdraw = Math.Max(thirdPartFee, _withdrawOptions.CurrentValue.MinThirdPartFee)
                .ToString(2, DecimalHelper.RoundingOption.Ceiling)
                .SafeToDecimal();
            AssertHelper.IsTrue(request.Amount >= minWithdraw, ErrorResult.AmountInsufficientCode);

            // Verify that the transaction information matches the order
            var transaction =
                Transaction.Parser.ParseFrom(ByteArrayHelper.HexStringToByteArray(request.RawTransaction));
            var resp = await VerifyTransactionAsync(request.FromChainId, userDto.Data.CaHash, transaction);
            AssertHelper.IsTrue(resp.Success, resp.Message);
            var transferTokenInput = resp.Value;

            var tokenGrain = _clusterClient.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(transferTokenInput.Symbol,
                request.FromChainId));
            var tokenDto = await tokenGrain.GetToken();
            _logger.LogDebug("Token info: {Token}", JsonConvert.SerializeObject(tokenDto));
            AssertHelper.NotNull(tokenDto, ErrorResult.SymbolInvalidCode, transferTokenInput.Symbol);
            AssertHelper.IsTrue(transferTokenInput.Symbol == request.Symbol, ErrorResult.SymbolInvalidCode, null,
                transferTokenInput.Symbol);

            var expectedAmount = request.Amount * (decimal)Math.Pow(10, tokenDto.Decimals);
            AssertHelper.IsTrue(transferTokenInput.Amount == expectedAmount, ErrorResult.AmountNotEqualCode);

            // Do create
            return await DoCreateOrderAsync(request, transaction, withdrawAmount, thirdPartFeeDto.AbsEstimateFee);
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
        var amountUsd = await CalculateAmountUsdAsync(request.Symbol, request.Amount);
        var tokenInfoGrain =
            _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(ITokenWithdrawLimitGrain.GenerateGrainId(request.Symbol));
        AssertHelper.IsTrue(await tokenInfoGrain.Acquire(amountUsd), ErrorResult.WithdrawLimitInsufficientCode, null,
            (await tokenInfoGrain.GetLimit()).RemainingLimit, TimeHelper.GetHourDiff(DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1).Date));
        try
        {
            var orderId = Guid.NewGuid();
            var grain = _clusterClient.GetGrain<IUserWithdrawGrain>(orderId);
            var withdrawOrderDto = new WithdrawOrderDto()
            {
                UserId = CurrentUser.GetId(),
                RawTransaction = request.RawTransaction,
                OrderType = OrderTypeEnum.Withdraw.ToString(),
                AmountUsd = amountUsd,
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
            await tokenInfoGrain.Reverse(amountUsd);
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
        AssertHelper.IsTrue(saveTransactionResult.Success, ErrorResult.TransactionFailCode);
    }

    private async Task<decimal> CalculateAmountUsdAsync(string symbol, decimal amount)
    {
        var amountUsd = 0M;
        try
        {
            var avgExchange =
                await _networkAppService.GetAvgExchangeAsync(symbol, CommonConstant.Symbol.USD);
            amountUsd = amount * avgExchange;
            _logger.LogDebug("CalculateAmountUsd: {symbol}, {amount}, {amountUsd}", symbol, amount, amountUsd);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Create withdraw order error in exchange usd, symbol: {symbol}", symbol);
        }

        AssertHelper.IsTrue(amountUsd > 0, ErrorResult.TransactionFailCode);
        return amountUsd;
    }

    public async Task<bool> AddOrUpdateAsync(WithdrawOrderDto dto)
    {
        try
        {
            await _withdrawOrderIndexRepository.AddOrUpdateAsync(
                _objectMapper.Map<WithdrawOrderDto, Orders.OrderIndex>(dto));
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
            TransferTokenInput transferTokenInput;
            switch (transaction.MethodName)
            {
                case ManagerForwardCall:
                    var caContractAddress1 = await _contractProvider.GetContractAddressAsync(chainId, CaContractName);
                    var caContractAddress2 = await _contractProvider.GetContractAddressAsync(chainId, CaContractName2);
                    AssertHelper.IsTrue(caContractAddress1 == transaction.To.ToBase58()
                                        || caContractAddress2 == transaction.To.ToBase58(),
                        "Invalid caContract address");

                    var param = ManagerForwardCallInput.Parser.ParseFrom(transaction.Params);
                    AssertHelper.IsTrue(param.MethodName == TransferToken, "Invalid ManagerForwardCall method {Mtd}",
                        param.MethodName);
                    AssertHelper.IsTrue(param.ContractAddress != new Address(), "Invalid ManagerForwardCall address");
                    AssertHelper.IsTrue(!param.Args.IsNullOrEmpty(), "Invalid ManagerForwardCall param");

                    var tokenPoolContractAddress =
                        await _contractProvider.GetContractAddressAsync(chainId, TokenPoolContractName);
                    AssertHelper.IsTrue(tokenPoolContractAddress == param.ContractAddress.ToBase58(),
                        "Invalid tokenPoolContract address");
                    AssertHelper.IsTrue(param.CaHash.ToHex() == caHash, "caHash not match");

                    transferTokenInput = TransferTokenInput.Parser.ParseFrom(param.Args);
                    AssertHelper.IsTrue(transferTokenInput.Amount > 0, "Tx Token amount {amount} invalid",
                        transferTokenInput.Amount);
                    break;
                case TransferToken:
                    tokenPoolContractAddress =
                        await _contractProvider.GetContractAddressAsync(chainId, TokenPoolContractName);
                    AssertHelper.IsTrue(tokenPoolContractAddress == transaction.To.ToBase58(),
                        "Invalid tokenPoolContract address");

                    transferTokenInput = TransferTokenInput.Parser.ParseFrom(transaction.Params);
                    AssertHelper.IsTrue(transferTokenInput.Amount > 0, "Tx Token amount {amount} invalid",
                        transferTokenInput.Amount);
                    break;
                default:
                    throw new UserFriendlyException("invalid method name");
            }
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