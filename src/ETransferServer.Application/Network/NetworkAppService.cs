using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.Network.Dtos;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Network;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class NetworkAppService : ETransferServerAppService, INetworkAppService
{
    private readonly ILogger<NetworkAppService> _logger;
    private readonly NetworkOptions _networkOptions;
    private readonly IObjectMapper _objectMapper;

    public NetworkAppService(ILogger<NetworkAppService> logger, IOptions<NetworkOptions> networkOptions,
        IObjectMapper objectMapper)
    {
        _logger = logger;
        _networkOptions = networkOptions.Value;
        _objectMapper = objectMapper;
    }

    public async Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request)
    {
        try
        {
            AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
            AssertHelper.NotEmpty(request.ChainId, "Invalid chainId. Please refresh and try again.");
            AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
            AssertHelper.NotEmpty(request.Symbol, "Invalid symbol. Please refresh and try again.");
            AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString()
                                || request.Type == OrderTypeEnum.Withdraw.ToString(), "Invalid type value. Please refresh and try again.");

            var networkConfigs = new List<NetworkConfig>();
            if (_networkOptions.NetworkMap.TryGetValue(request.Symbol, out var result))
            {
                AssertHelper.NotNull(request, "Symbol not exists. Please refresh and try again.");
                AssertHelper.NotEmpty(result, "Support network empty. Please refresh and try again.");
                networkConfigs = result.Where(a =>
                        a.SupportType.Contains(request.Type) && a.SupportChain.Contains(request.ChainId))
                    .ToList();
            }

            var networkInfos = networkConfigs.Select(config => config.NetworkInfo).ToList();
            var withdrawInfo = networkConfigs
                .Where(config => config.NetworkInfo != null && config.WithdrawInfo != null)
                .ToDictionary(config => config.NetworkInfo.Network, config => config.WithdrawInfo);

            var getNetworkListDto = new GetNetworkListDto();
            getNetworkListDto.ChainId = request.ChainId;
            
            getNetworkListDto.NetworkList = _objectMapper.Map<List<NetworkInfo>, List<NetworkDto>>(networkInfos);
            FillMultiConfirmMinutes(request.Type, getNetworkListDto.NetworkList, networkConfigs);

            // fill withdraw fee
            foreach (var networkDto in getNetworkListDto.NetworkList)
            {
                if (!withdrawInfo.TryGetValue(networkDto.Network, out var withdraw)) continue;
                networkDto.WithdrawFeeUnit = request.Symbol;
                networkDto.WithdrawFee = withdraw.WithdrawFee.ToString(CultureInfo.InvariantCulture);
            }
            
            if (request.Address.IsNullOrEmpty()) return getNetworkListDto;
            
            var networkByAddress = _networkOptions.NetworkPattern
                .Where(kv => request.Address.Match(kv.Key))
                .SelectMany(kv => kv.Value)
                .ToList();
            AssertHelper.NotEmpty(networkByAddress, "Please enter a correct address.");
            
            getNetworkListDto.NetworkList = getNetworkListDto.NetworkList
                .Where(net => net.Network.IsIn(networkByAddress))
                .ToList();
            AssertHelper.NotEmpty(getNetworkListDto.NetworkList, "{Networks} is currently not supported.",
                string.Join(CommonConstant.Slash, networkByAddress));

            return getNetworkListDto;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(e, "Get network list failed.");
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Get network list failed, type={Type}, chainId={ChainId}, address={Address}, symbol={Symbol}",
                request.Type, request.ChainId, request.Address, request.Symbol);
            throw;
        }
    }

    private void FillMultiConfirmMinutes(string type, List<NetworkDto> networkList, List<NetworkConfig> networkConfigs)
    {
        foreach (var networkDto in networkList)
        {
            var config = networkConfigs
                .Where(c => c.NetworkInfo != null)
                .FirstOrDefault(c => c.NetworkInfo.Network == networkDto.Network);
            if (config == null) continue;

            var multiConfirmSeconds = config.NetworkInfo.MultiConfirmSeconds;
            if (type == OrderTypeEnum.Deposit.ToString() && config.DepositInfo != null)
            {
                multiConfirmSeconds = config.DepositInfo.MultiConfirmSeconds;
            }

            if (type == OrderTypeEnum.Withdraw.ToString() && config.WithdrawInfo != null)
            {
                multiConfirmSeconds = config.WithdrawInfo.MultiConfirmSeconds;
            }
            
            networkDto.MultiConfirmTime = TimeHelper.SecondsToMinute((int)multiConfirmSeconds);
        }
    }
}