using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.Orders;
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
        
        if (!request.TxId.IsNullOrEmpty()) order.FromTransfer.TxId = request.TxId;
        if (!request.Status.IsNullOrEmpty() && request.Status.ToLower() == OrderOptions.Rejected) 
            order.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, OrderOperationStatusEnum.UserRejected.ToString());
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
        if (request.FromNetwork == ChainId.AELF || request.FromNetwork == ChainId.tDVV ||
            request.FromNetwork == ChainId.tDVW)
        {
            var result = await GetWithdrawInfoAsync(
                _objectMapper.Map<GetTransferListRequestDto, GetWithdrawListRequestDto>(request), version);
            return new GetTransferInfoDto
            {
                TransferInfo = _objectMapper.Map<WithdrawInfoDto, TransferDetailInfoDto>(result.WithdrawInfo)
            };
        }

        var userId = CurrentUser.GetId();
        AssertHelper.IsTrue(userId != Guid.Empty, "User not exists. Please refresh and try again.");
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.Symbol),
            "Symbol is not exist. Please refresh and try again.");
        AssertHelper.IsTrue(
            _networkInfoOptions.Value.NetworkMap[request.Symbol]
                .Exists(t => t.NetworkInfo.Network == request.FromNetwork),
            "FromNetwork is invalid. Please refresh and try again.");
        AssertHelper.IsTrue(
            string.IsNullOrWhiteSpace(request.Version) ||
            CommonConstant.DefaultConst.PortKeyVersion.Equals(request.Version) ||
            CommonConstant.DefaultConst.PortKeyVersion2.Equals(request.Version),
            "Version is invalid. Please refresh and try again.");
        AssertHelper.IsTrue(VerifyMemo(request.Memo), ErrorResult.MemoInvalidCode);
        
        if (!request.ToNetwork.IsNullOrEmpty())
        {
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
        withdrawInfoDto.TransactionFee = feeAmount.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
        withdrawInfoDto.TransactionUnit = request.Symbol;
        withdrawInfoDto.ExpiredTimestamp = expireAt.ToString();

        var receiveAmount = Math.Max(0, request.Amount) - decimal.Parse(withdrawInfoDto.TransactionFee);
        var minAmount = feeAmount;
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
            var fee = feeAmount * avgExchange;
            withdrawInfoDto.FeeUsd = fee.ToString(decimals, DecimalHelper.RoundingOption.Ceiling);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get transfer avg exchange failed.");
        }

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
        if (request.FromNetwork == ChainId.AELF || request.FromNetwork == ChainId.tDVV ||
            request.FromNetwork == ChainId.tDVW)
        {
            var result = await CreateWithdrawOrderInfoAsync(
                _objectMapper.Map<GetTransferOrderRequestDto, GetWithdrawOrderRequestDto>(request), version);
            return _objectMapper.Map<CreateWithdrawOrderDto, CreateTransferOrderDto>(result);
        }
        
        _logger.LogDebug("CreateTransferOrder: {request}", JsonConvert.SerializeObject(request));
        var userId = CurrentUser.GetId();
        AssertHelper.IsTrue(_networkInfoOptions.Value.NetworkMap.ContainsKey(request.FromSymbol),
            ErrorResult.SymbolInvalidCode, null, request.FromSymbol);
        AssertHelper.IsTrue(request.FromSymbol == request.ToSymbol, 
            "Symbol is invalid. Please refresh and try again.");
        AssertHelper.IsTrue(await IsAddressSupport(request.FromNetwork, request.FromSymbol, request.ToAddress, version),
            ErrorResult.AddressInvalidCode);
        AssertHelper.IsTrue(IsNetworkOpen(request.ToSymbol, request.ToNetwork, OrderTypeEnum.Transfer.ToString()), 
            ErrorResult.CoinSuspendedTemporarily);
        AssertHelper.IsTrue(VerifyMemo(request.Memo), ErrorResult.MemoInvalidCode);
        
        var networkConfig = _networkInfoOptions.Value.NetworkMap[request.FromSymbol]
            .FirstOrDefault(t => t.NetworkInfo.Network == request.FromNetwork);
        AssertHelper.NotNull(networkConfig, ErrorResult.NetworkInvalidCode);
        AssertHelper.IsTrue(await VerifyByVersionAndWhiteList(networkConfig, userId, version), ErrorResult.VersionOrWhitelistVerifyFailCode);
        networkConfig = _networkInfoOptions.Value.NetworkMap[request.ToSymbol]
            .FirstOrDefault(t => t.NetworkInfo.Network == request.ToNetwork);
        AssertHelper.NotNull(networkConfig, ErrorResult.NetworkInvalidCode);
        AssertHelper.IsTrue(await VerifyByVersionAndWhiteList(networkConfig, userId, version), ErrorResult.VersionOrWhitelistVerifyFailCode);

        if (VerifyAElfChain(request.ToNetwork))
        {
            AssertHelper.IsTrue(VerifyHelper.VerifyAelfAddress(request.ToAddress), ErrorResult.AddressFormatWrongCode);
        }

        var userGrain = _clusterClient.GetGrain<IUserGrain>(userId);
        var userDto = await userGrain.GetUser();
        AssertHelper.IsTrue(userDto.Success, ErrorResult.JwtInvalidCode);

        var thirdPartFee = 0M;
        var coBoCoinCacheKey =
            CacheKey(FeeInfo.FeeName.CoBoFee, userId.ToString(), request.ToNetwork, request.ToSymbol);
        var thirdPartFeeDto = await _coBoCoinCache.GetAsync(coBoCoinCacheKey);
        AssertHelper.IsTrue(thirdPartFeeDto != null, ErrorResult.FeeExpiredCode);
        _logger.LogDebug("Cobo fee get cache: {fee}", thirdPartFeeDto.AbsEstimateFee);
        var inputThirdPartFee = thirdPartFeeDto.AbsEstimateFee.SafeToDecimal(-1);
        AssertHelper.IsTrue(inputThirdPartFee >= 0, ErrorResult.FeeInvalidCode);
        
        if (!VerifyAElfChain(request.ToNetwork))
        { 
            // query fees async
            thirdPartFee = (await CalculateThirdPartFeeAsync(userId, request.ToNetwork, request.ToSymbol)).Item1;
            AssertHelper.IsTrue(
                Math.Abs(inputThirdPartFee - thirdPartFee) / thirdPartFee <=
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
    
    private async Task<OrderIndex> GetOrderIndexAsync(string orderId)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i => i.Field(f => f.Id).Value(orderId)));

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        return await _withdrawOrderIndexRepository.GetAsync(Filter);
    }
}