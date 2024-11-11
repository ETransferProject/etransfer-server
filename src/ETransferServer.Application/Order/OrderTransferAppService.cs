using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Withdraw.Dtos;
using Microsoft.Extensions.Logging;
using Volo.Abp.Users;

namespace ETransferServer.Order;

public partial class OrderWithdrawAppService
{
    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderWithdrawAppService),
        MethodName = nameof(HandleGetTransferInfoExceptionAsync))]
    public async Task<GetWithdrawInfoDto> GetTransferInfoAsync(GetTransferListRequestDto request, string version = null)
    {
        if (request.FromNetwork == ChainId.AELF || request.FromNetwork == ChainId.tDVV ||
            request.FromNetwork == ChainId.tDVW)
        {
            return await GetWithdrawInfoAsync(
                _objectMapper.Map<GetTransferListRequestDto, GetWithdrawListRequestDto>(request), version);
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
        var withdrawInfoDto = new WithdrawInfoDto();
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
            return new GetWithdrawInfoDto { WithdrawInfo = withdrawInfoDto };

        AssertHelper.IsTrue(await IsAddressSupport(request.FromNetwork, request.Symbol, request.ToAddress, version),
            ErrorResult.AddressFormatWrongCode);
        return new GetWithdrawInfoDto
        {
            WithdrawInfo = withdrawInfoDto
        };
    }
}