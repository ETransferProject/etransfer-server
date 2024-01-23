using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.Network.Dtos;
using Orleans;
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
    private readonly IClusterClient _clusterClient;
    private const int ThirdPartDecimals = 6;

    public NetworkAppService(ILogger<NetworkAppService> logger, 
        IOptions<NetworkOptions> networkOptions,
        IObjectMapper objectMapper,
        IClusterClient clusterClient)
    {
        _logger = logger;
        _networkOptions = networkOptions.Value;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
    }

    public async Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request)
    {
        try
        {
            var getNetworkListDto = await GetNetworkListWithoutFeeAsync(request);

            // fill withdraw fee
            foreach (var networkDto in getNetworkListDto.NetworkList)
            {
                networkDto.WithdrawFeeUnit = request.Symbol;
                try
                {
                    networkDto.WithdrawFee = (await CalculateNetworkFeeAsync(networkDto.Network, request.Symbol)).Item1
                        .ToString(ThirdPartDecimals, DecimalHelper.RoundingOption.Ceiling);
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        "Get withdraw fee failed, network={Network}, symbol={Symbol}",
                        networkDto.Network, request.Symbol);
                }
            }
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

    public async Task<GetNetworkListDto> GetNetworkListWithoutFeeAsync(GetNetworkListRequestDto request)
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

        var getNetworkListDto = new GetNetworkListDto();
        getNetworkListDto.ChainId = request.ChainId;
        
        getNetworkListDto.NetworkList = _objectMapper.Map<List<NetworkInfo>, List<NetworkDto>>(networkInfos);
        FillMultiConfirmMinutes(request.Type, getNetworkListDto.NetworkList, networkConfigs);

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

    public async Task<Tuple<decimal, CoBoCoinDto>> CalculateNetworkFeeAsync(string network, string symbol)
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
        return Tuple.Create(estimateFee, coin);
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