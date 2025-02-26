using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.Orders;
using ETransferServer.Users;
using ETransferServer.Withdraw.Dtos;
using ETransferServer.WithdrawOrder.Dtos;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Nest;
using Newtonsoft.Json;
using Volo.Abp;
using Volo.Abp.Users;

namespace ETransferServer.Order;

public partial class OrderWithdrawAppService
{
    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderWithdrawAppService),
        MethodName = nameof(HandleSaveTransferExceptionAsync))]
    public async Task<bool> SaveTransferOrderInfoAsync(string orderId, GetTransferOrderInfoRequestDto request)
    {
        if (orderId.IsNullOrEmpty() || !Guid.TryParse(orderId, out _)) return false;
        var recordGrain = _clusterClient.GetGrain<IUserWithdrawRecordGrain>(Guid.Parse(orderId));
        var order = (await recordGrain.Get())?.Value;
        if (order == null) return false;
        if (CurrentUser.GetId() != order.UserId
            || order.FromTransfer.Amount != request.Amount
            || order.FromTransfer.Network != request.FromNetwork
            || order.ToTransfer.Network != (VerifyAElfChain(request.ToNetwork) ? CommonConstant.Network.AElf : request.ToNetwork)
            || order.FromTransfer.Symbol != request.FromSymbol
            || order.ToTransfer.Symbol != request.ToSymbol
            || order.FromTransfer.FromAddress != request.FromAddress
            || order.ToTransfer.ToAddress != request.ToAddress) return false;
        if (!request.Address.IsNullOrEmpty() && order.FromTransfer.ToAddress != request.Address) return false;
        
        if (!request.TxId.IsNullOrEmpty() && order.ThirdPartOrderId.IsNullOrEmpty()) order.FromTransfer.TxId = request.TxId;
        if (!request.Status.IsNullOrEmpty() && request.Status.ToLower() == OrderOptions.Rejected)
        {
            order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, OrderOperationStatusEnum.UserTransferRejected.ToString());
            await RecycleAddressAsync(order);
        }

        await recordGrain.AddOrUpdate(order);
        var orderIndex = await GetOrderIndexAsync(orderId);
        orderIndex = _objectMapper.Map<WithdrawOrderDto, OrderIndex>(order);
        await _withdrawOrderIndexRepository.AddOrUpdateAsync(orderIndex);
        return true;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderWithdrawAppService),
        MethodName = nameof(HandleGetTransferInfoExceptionAsync))]
    public async Task<GetTransferInfoDto> GetTransferInfoAsync(GetTransferListRequestDto request, string version = null)
    {
        AssertHelper.IsTrue(await CheckNetworkAsync(request), ErrorResult.NetworkInvalidCode);
        if (VerifyAElfChain(request.FromNetwork))
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await GetWithdrawInfoAsync(
                _objectMapper.Map<GetTransferListRequestDto, GetWithdrawListRequestDto>(request), version);
            var transferInfo = _objectMapper.Map<WithdrawInfoDto, TransferDetailInfoDto>(result.WithdrawInfo);
            transferInfo.ContractAddress = _networkInfoOptions.Value.NetworkMap[request.Symbol]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.FromNetwork)?.NetworkInfo?.ContractAddress
                ?? _networkInfoOptions.Value.NetworkMap[CommonConstant.Symbol.USDT]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.FromNetwork)?.NetworkInfo?.ContractAddress;
            _logger.LogInformation("Get transfer info cost time: {time}", stopwatch.ElapsedMilliseconds);
            return new GetTransferInfoDto
            {
                TransferInfo = transferInfo
            };
        }
        
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.Symbol),
            ErrorResult.SymbolInvalidCode, null, request.Symbol);
        AssertHelper.IsTrue(
            _networkInfoOptions.Value.NetworkMap[request.Symbol]
                .Exists(t => t.NetworkInfo.Network == request.FromNetwork),
            ErrorResult.NetworkInvalidCode);
        AssertHelper.IsTrue(
            string.IsNullOrWhiteSpace(request.Version) ||
            CommonConstant.DefaultConst.PortKeyVersion.Equals(request.Version) ||
            CommonConstant.DefaultConst.PortKeyVersion2.Equals(request.Version),
            ErrorResult.VersionOrWhitelistVerifyFailCode);
        AssertHelper.IsTrue(VerifyMemo(request.Memo), ErrorResult.MemoInvalidCode);
        
        var userId = await GetUserIdAsync(request.SourceType, request.FromAddress);
        if (!request.ToNetwork.IsNullOrEmpty() && !VerifyAElfChain(request.ToNetwork))
        {
            AssertHelper.IsTrue(request.FromNetwork != request.ToNetwork, ErrorResult.NetworkInvalidCode);
            var networkConfig = _networkInfoOptions.Value.NetworkMap[request.Symbol]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.ToNetwork);
            AssertHelper.NotNull(networkConfig, ErrorResult.NetworkInvalidCode);
            AssertHelper.IsTrue(await VerifyByVersionAndWhiteList(networkConfig, userId, version),
                ErrorResult.VersionOrWhitelistVerifyFailCode);
        }
        if (VerifyAElfChain(request.ToNetwork))
        {
            AssertHelper.IsTrue(VerifyHelper.VerifyAelfAddress(request.ToAddress), ErrorResult.AddressFormatWrongCode);
        }

        var tokenInfoGrain =
            _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(
                ITokenWithdrawLimitGrain.GenerateGrainId(request.Symbol));

        var tokenLimit = await tokenInfoGrain.GetLimit();
        var withdrawInfoDto = new TransferDetailInfoDto();
        withdrawInfoDto.ContractAddress = _networkInfoOptions.Value.NetworkMap[request.Symbol]
            .FirstOrDefault(t => t.NetworkInfo.Network == request.FromNetwork).NetworkInfo.ContractAddress;
        withdrawInfoDto.LimitCurrency = request.Symbol;
        withdrawInfoDto.TransactionUnit = request.Symbol;

        // query async
        var decimals = await _networkAppService.GetDecimalsAsync(request.FromNetwork, request.Symbol);
        var (feeAmount, expireAt) = (0M,
            DateTime.UtcNow.AddSeconds(_coBoOptions.Value.CoinExpireSeconds).ToUtcMilliSeconds());
        withdrawInfoDto.TransactionFee = feeAmount.ToString();
        if (!VerifyAElfChain(request.ToNetwork))
        {
            var thirdPartFeeTask = request.ToNetwork.IsNullOrEmpty()
                ? CalculateThirdPartFeesWithCacheAsync(userId, request.Symbol, version)
                : CalculateThirdPartFeeAsync(userId, request.ToNetwork, request.Symbol);

            (feeAmount, expireAt) = await thirdPartFeeTask;
        }

        feeAmount = await GetTransactionFeeAsync(_objectMapper.Map<GetTransferListRequestDto, GetWithdrawListRequestDto>(request), userId, feeAmount);
        var (totalFee, totalExpireAt) = await CalculateTotalFeeAsync(userId, request.FromNetwork, request.ToNetwork, request.Symbol, expireAt, feeAmount);
        withdrawInfoDto.TransactionFee = totalFee.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
        withdrawInfoDto.TransactionUnit = request.Symbol;
        withdrawInfoDto.ExpiredTimestamp = totalExpireAt.ToString();

        var receiveAmount = Math.Max(0, request.Amount) - decimal.Parse(withdrawInfoDto.TransactionFee);
        var minAmount = totalFee;
        withdrawInfoDto.MinAmount = Math.Max(minAmount, _withdrawInfoOptions.Value.MinWithdraw)
            .ToString(2, DecimalHelper.RoundingOption.Ceiling);
        if (withdrawInfoDto.MinAmount.SafeToDecimal() <= withdrawInfoDto.TransactionFee.SafeToDecimal())
        {
            withdrawInfoDto.MinAmount = (withdrawInfoDto.MinAmount.SafeToDecimal() + 0.01M).ToString();
        }

        withdrawInfoDto.ReceiveAmount = receiveAmount > 0
            ? receiveAmount.ToString(decimals, DecimalHelper.RoundingOption.Ceiling)
            : withdrawInfoDto.ReceiveAmount ?? default(int).ToString();
        try
        {
            var avgExchange =
                await _networkAppService.GetAvgExchangeAsync(request.Symbol, CommonConstant.Symbol.USD);
            withdrawInfoDto.TotalLimit =
                (_networkInfoOptions.Value.WithdrawLimit24H / avgExchange).ToString(decimals,
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
            var fee = totalFee * avgExchange;
            withdrawInfoDto.FeeUsd = fee.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get transfer avg exchange failed.");
        }

        AssertHelper.IsTrue(request.Amount >= 0 && request.Amount <= withdrawInfoDto.MaxAmount.SafeToDecimal(), 
            ErrorResult.AmountNotEqualCode);
        if (request.ToAddress.IsNullOrEmpty())
            return new GetTransferInfoDto { TransferInfo = withdrawInfoDto };

        AssertHelper.IsTrue(await IsAddressSupport(request.FromNetwork, request.Symbol, request.ToAddress, version),
            ErrorResult.AddressFormatWrongCode);
        return new GetTransferInfoDto
        {
            TransferInfo = withdrawInfoDto
        };
    }
    
    [ExceptionHandler(typeof(UserFriendlyException), typeof(Exception), 
        TargetType = typeof(OrderWithdrawAppService), MethodName = nameof(HandleCreateTransferExceptionAsync))]
    public async Task<CreateTransferOrderDto> CreateTransferOrderInfoAsync(GetTransferOrderRequestDto request, string version = null)
    {
        AssertHelper.IsTrue(await CheckNetworkAsync(request), ErrorResult.NetworkInvalidCode);
        if (VerifyAElfChain(request.FromNetwork))
        {
            var result = await CreateWithdrawOrderInfoAsync(
                _objectMapper.Map<GetTransferOrderRequestDto, GetWithdrawOrderRequestDto>(request), version, true);
            return _objectMapper.Map<CreateWithdrawOrderDto, CreateTransferOrderDto>(result);
        }
        
        _logger.LogDebug("CreateTransferOrder: {request}", JsonConvert.SerializeObject(request));
        var userId = CurrentUser.GetId();
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.FromSymbol),
            ErrorResult.SymbolInvalidCode, null, request.FromSymbol);
        AssertHelper.IsTrue(request.FromSymbol == request.ToSymbol, 
            ErrorResult.SymbolInvalidCode, null, request.FromSymbol);
        AssertHelper.IsTrue(await IsAddressSupport(request.FromNetwork, request.FromSymbol, request.ToAddress, version),
            ErrorResult.AddressInvalidCode);

        if (!VerifyAElfChain(request.ToNetwork))
        {
            AssertHelper.IsTrue(IsNetworkOpen(request.ToSymbol, request.ToNetwork, OrderTypeEnum.Transfer.ToString()),
                ErrorResult.CoinSuspendedTemporarily);
            AssertHelper.IsTrue(request.FromNetwork != request.ToNetwork, ErrorResult.NetworkInvalidCode);
            var toNetworkConfig = _networkInfoOptions.Value.NetworkMap[request.ToSymbol]
                .FirstOrDefault(t => t.NetworkInfo.Network == request.ToNetwork);
            AssertHelper.NotNull(toNetworkConfig, ErrorResult.NetworkInvalidCode);
            AssertHelper.IsTrue(await VerifyByVersionAndWhiteList(toNetworkConfig, userId, version), ErrorResult.VersionOrWhitelistVerifyFailCode);
        }
        else
        {
            AssertHelper.IsTrue(VerifyHelper.VerifyAelfAddress(request.ToAddress), ErrorResult.AddressFormatWrongCode);
        }

        AssertHelper.IsTrue(VerifyMemo(request.Memo), ErrorResult.MemoInvalidCode);
        
        var networkConfig = _networkInfoOptions.Value.NetworkMap[request.FromSymbol]
            .FirstOrDefault(t => t.NetworkInfo.Network == request.FromNetwork);
        AssertHelper.NotNull(networkConfig, ErrorResult.NetworkInvalidCode);
        AssertHelper.IsTrue(await VerifyByVersionAndWhiteList(networkConfig, userId, version), ErrorResult.VersionOrWhitelistVerifyFailCode);

        var userGrain = _clusterClient.GetGrain<IUserGrain>(userId);
        var userDto = await userGrain.GetUser();
        AssertHelper.IsTrue(userDto.Success, ErrorResult.JwtInvalidCode);
        var userAddress = userDto.Data?.AddressInfos?.FirstOrDefault()?.Address;
        AssertHelper.IsTrue(!userAddress.IsNullOrEmpty() && userAddress.EndsWith(request.FromAddress),
            ErrorResult.AddressInvalidCode);

        var thirdPartFee = 0M;
        var coBoCoinCacheKey =
            CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), request.FromNetwork, request.ToNetwork, request.ToSymbol);
        var thirdPartFeeDto = await _coBoCoinCache.GetAsync(coBoCoinCacheKey);
        AssertHelper.IsTrue(thirdPartFeeDto != null, ErrorResult.FeeExpiredCode);
        _logger.LogDebug("Cobo total fee get transfer cache: {fee}, {userId}, {fromNetwork}, {toNetwork}, {symbol}", 
            thirdPartFeeDto.AbsEstimateFee, userId, request.FromNetwork, request.ToNetwork, request.ToSymbol);
        var inputThirdPartFee = thirdPartFeeDto.AbsEstimateFee.SafeToDecimal(-1);
        AssertHelper.IsTrue(inputThirdPartFee >= 0, ErrorResult.FeeInvalidCode);
        
        if (!VerifyAElfChain(request.ToNetwork))
        { 
            // query fees async
            var toKey =
                CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), request.ToNetwork, request.ToSymbol);
            var toFeeDto = await _coBoCoinCache.GetAsync(toKey);
            AssertHelper.IsTrue(toFeeDto != null, ErrorResult.FeeExpiredCode);
            _logger.LogDebug("Cobo fee get transfer cache: {fee}, {userId}, {toNetwork}, {symbol}", 
                toFeeDto.AbsEstimateFee, userId, request.ToNetwork, request.ToSymbol);
            var toFee = toFeeDto.AbsEstimateFee.SafeToDecimal(-1);
            AssertHelper.IsTrue(toFee >= 0, ErrorResult.FeeInvalidCode);
            
            thirdPartFee = (await CalculateThirdPartFeeAsync(userId, request.ToNetwork, request.ToSymbol)).Item1;
            AssertHelper.IsTrue(
                Math.Abs(toFee - thirdPartFee) / thirdPartFee <=
                _withdrawInfoOptions.Value.FeeFluctuationPercent,
                ErrorResult.FeeExceedCode, null, request.ToNetwork);
        }

        // withdraw fee to thirdPart
        var withdrawAmount = request.Amount - inputThirdPartFee;
        AssertHelper.IsTrue(withdrawAmount > 0, ErrorResult.AmountInsufficientCode);

        var minWithdraw = Math.Max(thirdPartFee, _withdrawInfoOptions.Value.MinWithdraw)
            .ToString(2, DecimalHelper.RoundingOption.Ceiling)
            .SafeToDecimal();
        AssertHelper.IsTrue(request.Amount >= minWithdraw, ErrorResult.AmountInsufficientCode);

        // Do create
        return await DoCreateOrderAsync(request, withdrawAmount, inputThirdPartFee.ToString());
    }
    
    private async Task<CreateTransferOrderDto> DoCreateOrderAsync(GetTransferOrderRequestDto request,
        decimal withdrawAmount, string feeStr)
    {
        var orderId = Guid.NewGuid();
        var userAddressGrain = _clusterClient.GetGrain<IUserDepositAddressGrain>(
            GuidHelper.GenerateId(request.FromNetwork, request.FromSymbol, orderId.ToString()));
        var address = await userAddressGrain.GetTransferAddress();
        AssertHelper.IsTrue(!address.IsNullOrEmpty(), ErrorResult.TransactionFailCode);
        
        // amount limit
        var amountUsd = await CalculateAmountUsdAsync(request.FromSymbol, request.Amount);
        var tokenInfoGrain =
            _clusterClient.GetGrain<ITokenWithdrawLimitGrain>(ITokenWithdrawLimitGrain.GenerateGrainId(request.FromSymbol));
        AssertHelper.IsTrue(await tokenInfoGrain.Acquire(amountUsd), ErrorResult.WithdrawLimitInsufficientCode, null,
            (await tokenInfoGrain.GetLimit()).RemainingLimit, TimeHelper.GetHourDiff(DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1).Date));
        try
        {
            var transferGrain = _clusterClient.GetGrain<IUserWithdrawGrain>(orderId);
            var withdrawOrderDto = new WithdrawOrderDto
            {
                UserId = CurrentUser.GetId(),
                OrderType = OrderTypeEnum.Withdraw.ToString(),
                AmountUsd = amountUsd,
                FromTransfer = new TransferInfo
                {
                    Network = request.FromNetwork,
                    Amount = request.Amount,
                    Symbol = request.FromSymbol,
                    FromAddress = request.FromAddress,
                    ToAddress = address
                },
                ToTransfer = new TransferInfo
                {
                    Network = VerifyAElfChain(request.ToNetwork) ? CommonConstant.Network.AElf : request.ToNetwork,
                    ChainId = VerifyAElfChain(request.ToNetwork) ? request.ToNetwork : string.Empty,
                    ToAddress = request.ToAddress,
                    Amount = withdrawAmount,
                    Symbol = request.ToSymbol,
                    FeeInfo = new List<FeeInfo>
                    {
                        new(request.ToSymbol, feeStr)
                    }
                }
            };
            withdrawOrderDto.ExtensionInfo = new Dictionary<string, string>();
            withdrawOrderDto.ExtensionInfo.Add(ExtensionKey.OrderType, OrderTypeEnum.Transfer.ToString());

            if (!string.IsNullOrWhiteSpace(request.Memo))
            {
                withdrawOrderDto.ExtensionInfo.Add(ExtensionKey.Memo, request.Memo);
            }
            
            await transferGrain.CreateTransferOrder(withdrawOrderDto);
            await AddCheckOrder(orderId.ToString());
            return new CreateTransferOrderDto
            {
                OrderId = orderId.ToString(),
                Address = address
            };
        }
        catch (Exception e)
        {
            await tokenInfoGrain.Reverse(amountUsd);
            _logger.LogError(e,
                "Create transfer order error, fromNetwork:{FromNetwork}, toNetwork:{ToNetwork}, toAddress:{ToAddress}, amount:{Amount}, symbol:{Symbol}",
                request.FromNetwork, request.ToNetwork, request.ToAddress, request.Amount, request.FromSymbol);
            throw;
        }
    }
    
    public async Task AddCheckOrder(string id)
    {
        var transferReminderGrain =
            _clusterClient.GetGrain<ITransferOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(ITransferOrderStatusReminderGrain)));
        await transferReminderGrain.AddReminder(id);
    }
    
    private async Task<OrderIndex> GetOrderIndexAsync(string orderId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Id).Value(orderId)));

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _withdrawOrderIndexRepository.GetAsync(Filter);
    }
    
    private async Task RecycleAddressAsync(WithdrawOrderDto order)
    {
        var addressKey = GuidHelper.GenerateId(order.FromTransfer.Network, order.FromTransfer.Symbol);
        if (!_depositInfoOptions.Value.TransferAddressLists.IsNullOrEmpty() &&
            _depositInfoOptions.Value.TransferAddressLists.ContainsKey(addressKey) &&
            _depositInfoOptions.Value.TransferAddressLists[addressKey]
                .Contains(order.FromTransfer.ToAddress)) return;
        
        _logger.LogInformation("Address recycle when rejected: {orderId}, {address}", order.Id, order.FromTransfer.ToAddress);
        var addressGrain = _clusterClient.GetGrain<IUserTokenDepositAddressGrain>(order.FromTransfer.ToAddress);
        var userAddressDto = (await addressGrain.Get())?.Value;
        if (userAddressDto == null) return;
        
        var addressLimitGrain = _clusterClient.GetGrain<ITokenAddressLimitGrain>(
            GuidHelper.UniqGuid(nameof(ITokenAddressLimitGrain)));
        await addressLimitGrain.Reverse();
        userAddressDto.IsAssigned = false;
        userAddressDto.OrderId = string.Empty;
        userAddressDto.UpdateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow);
        await addressGrain.AddOrUpdate(userAddressDto);
        await _userAddressIndexRepository.AddOrUpdateAsync(_objectMapper.Map<UserAddressDto, UserAddress>(userAddressDto));
    }
    
    private async Task<bool> CheckNetworkAsync(GetTransferListRequestDto request)
    {
        if (!request.FromNetwork.IsNullOrEmpty() && !request.SourceType.IsNullOrEmpty() && !request.FromAddress.IsNullOrEmpty())
        {
            WalletEnum walletType;
            if (!Enum.TryParse<WalletEnum>(request.SourceType, true, out walletType)) return false;
            if ((int)walletType <= 1 && (!VerifyAElfChain(request.FromNetwork) || !VerifyHelper.VerifyAelfAddress(request.FromAddress)))
                return false;
            var networkByAddress = _networkInfoOptions.Value.NetworkPattern
                .Where(kv => request.FromAddress.Match(kv.Key))
                .SelectMany(kv => kv.Value)
                .ToList();
            AssertHelper.NotEmpty(networkByAddress, ErrorResult.AddressFormatWrongCode);
            if (!networkByAddress.Exists(t => t == request.FromNetwork)) return false;
        }

        if (!request.ToNetwork.IsNullOrEmpty() && !request.ToAddress.IsNullOrEmpty())
        {
            var networkByAddress = _networkInfoOptions.Value.NetworkPattern
                .Where(kv => request.ToAddress.Match(kv.Key))
                .SelectMany(kv => kv.Value)
                .ToList();
            AssertHelper.NotEmpty(networkByAddress, ErrorResult.AddressFormatWrongCode);
            if (!networkByAddress.Exists(t => t == request.ToNetwork)) return false;
        }

        return true;
    }
    
    private async Task<bool> CheckNetworkAsync(GetTransferOrderRequestDto request)
    {
        var networkByAddress = _networkInfoOptions.Value.NetworkPattern
            .Where(kv => request.FromAddress.Match(kv.Key))
            .SelectMany(kv => kv.Value)
            .ToList();
        AssertHelper.NotEmpty(networkByAddress, ErrorResult.AddressFormatWrongCode);
        if (!networkByAddress.Exists(t => t == request.FromNetwork)) return false;
        
        networkByAddress = _networkInfoOptions.Value.NetworkPattern
            .Where(kv => request.ToAddress.Match(kv.Key))
            .SelectMany(kv => kv.Value)
            .ToList();
        AssertHelper.NotEmpty(networkByAddress, ErrorResult.AddressFormatWrongCode);
        if (!networkByAddress.Exists(t => t == request.ToNetwork)) return false;

        return true;
    }

    private async Task<Guid?> GetUserIdAsync(string sourceType, string address)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (userId.HasValue) return userId;
        if (sourceType.IsNullOrEmpty() || address.IsNullOrEmpty()
                                       || !Enum.TryParse<WalletEnum>(sourceType, true, out _)) return null;

        if (Enum.TryParse<WalletEnum>(sourceType, true, out var walletType)
            && (int)walletType > 1)
        {
            var fullAddress = string.Concat(sourceType.ToLower(), CommonConstant.Underline, address);
            userId = GuidHelper.UniqGuid(fullAddress);
            _logger.LogInformation("GetUserId from wallet, {sourceType}, {address}, {userId}", 
                sourceType, address, userId);
            return userId;
        }
        
        var user = await _userAppService.GetUserByAddressAsync(address);
        _logger.LogInformation("GetUserId from portkey or nightElf, {sourceType}, {address}, {userId}, {newUserId}",
            sourceType, address, user?.Id, GuidHelper.UniqGuid(address));
        return user?.Id ?? GuidHelper.UniqGuid(address);
    }
}