using System;
using System.Collections.Generic;
using System.Globalization;
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
using ETransferServer.ThirdPart.Exchange;
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
    private readonly CoinGeckoOptions _coinGeckoOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private const int ThirdPartDigitals = 4;

    public NetworkAppService(ILogger<NetworkAppService> logger, 
        IOptionsSnapshot<NetworkOptions> networkOptions,
        IOptionsSnapshot<CoinGeckoOptions> coinGeckoOptions,
        IObjectMapper objectMapper,
        IClusterClient clusterClient)
    {
        _logger = logger;
        _networkOptions = networkOptions.Value;
        _coinGeckoOptions = coinGeckoOptions.Value;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
    }

    public async Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request)
    {
        try
        {
            var getNetworkListDto = await GetNetworkListWithLocalFeeAsync(request);
            if (request.Type == OrderTypeEnum.Deposit.ToString()) return getNetworkListDto;
            
            // fill withdraw fee
            foreach (var networkDto in getNetworkListDto.NetworkList)
            {
                networkDto.WithdrawFee = await GetCacheFeeAsync(networkDto.Network, request.Symbol) ??
                                         networkDto.WithdrawFee;
            }

            try
            {
                getNetworkListDto.NetworkList = request.Symbol == CommonConstant.Symbol.USDT ?
                    await CalculateNetworkFeeListAsync(getNetworkListDto.NetworkList, request.Symbol)
                    : await CalculateAvgNetworkFeeListAsync(getNetworkListDto.NetworkList, request.Symbol);
            }
            catch (Exception e)
            {
                foreach (var networkDto in getNetworkListDto.NetworkList)
                {
                    networkDto.WithdrawFee = null;
                    networkDto.WithdrawFeeUnit = request.Symbol;
                }
                _logger.LogError(e, "Get withdraw fee failed by exchange.");
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

    public async Task<GetNetworkListDto> GetNetworkListWithLocalFeeAsync(GetNetworkListRequestDto request)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.ChainId, "Invalid chainId. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Symbol, "Invalid symbol. Please refresh and try again.");
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString()
                            || request.Type == OrderTypeEnum.Withdraw.ToString(),
            "Invalid type value. Please refresh and try again.");
        AssertHelper.IsTrue(_networkOptions.NetworkMap.ContainsKey(request.Symbol),
            "Symbol is not exist. Please refresh and try again.");

        var networkConfigs = _networkOptions.NetworkMap[request.Symbol].Where(a =>
                a.SupportType.Contains(request.Type) && a.SupportChain.Contains(request.ChainId))
            .ToList();

        var networkInfos = networkConfigs.Select(config => config.NetworkInfo).ToList();
        var withdrawInfo = networkConfigs
            .Where(config => config.NetworkInfo != null && config.WithdrawInfo != null)
            .ToDictionary(config => config.NetworkInfo.Network, config => config.WithdrawInfo);
        var getNetworkListDto = new GetNetworkListDto();
        getNetworkListDto.ChainId = request.ChainId;

        getNetworkListDto.NetworkList = _objectMapper.Map<List<NetworkInfo>, List<NetworkDto>>(networkInfos);
        FillMultiConfirmMinutes(request.Type, getNetworkListDto.NetworkList, networkConfigs);

        foreach (var networkDto in getNetworkListDto.NetworkList)
        {
            if (request.Type == OrderTypeEnum.Deposit.ToString() ||
                !withdrawInfo.TryGetValue(networkDto.Network, out var withdraw)) continue;
            networkDto.WithdrawFeeUnit = withdraw.WithdrawLocalFeeUnit;
            networkDto.WithdrawFee = withdraw.WithdrawLocalFee.ToString(CultureInfo.InvariantCulture);
        }

        if (request.Address.IsNullOrEmpty()) return getNetworkListDto;

        var networkByAddress = _networkOptions.NetworkPattern
            .Where(kv => request.Address.Match(kv.Key))
            .SelectMany(kv => kv.Value)
            .ToList();
        AssertHelper.NotEmpty(networkByAddress, ErrorResult.AddressFormatWrongCode);

        getNetworkListDto.NetworkList = getNetworkListDto.NetworkList
            .Where(net => net.Network.IsIn(networkByAddress))
            .ToList();
        AssertHelper.NotEmpty(getNetworkListDto.NetworkList, ErrorResult.NetworkNotSupportCode,
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

        var avgExchange = await GetAvgExchangeAsync(feeSymbol, symbol);
        var estimateFee = coin.AbsEstimateFee.SafeToDecimal() * avgExchange;
        return Tuple.Create(estimateFee, coin);
    }
    
    public async Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol)
    {
        var exchangeSymbolPair = string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
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
        return avgExchange;
    }

    private async Task<List<NetworkDto>> CalculateNetworkFeeListAsync(List<NetworkDto> networkList, string symbol)
    {
        var exchangeSymbolPair = networkList.ConvertAll(item => item.Network).ToList().JoinAsString(CommonConstant.Underline);
        var exchangeGrain = _clusterClient.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
        var exchange = await exchangeGrain.GetByProviderAsync(ExchangeProviderName.CoinGecko, symbol);
        AssertHelper.NotEmpty(exchange, "Exchange data list not found {}", exchangeSymbolPair);

        foreach (var network in networkList)
        {
            network.WithdrawFee = (network.WithdrawFee.SafeToDecimal() * exchange[_coinGeckoOptions.CoinIdMapping[network.WithdrawFeeUnit]].Exchange)
                .ToString(ThirdPartDigitals, DecimalHelper.GetDecimals(symbol), DecimalHelper.RoundingOption.Ceiling);
            network.WithdrawFeeUnit = symbol;
        }

        return networkList;
    }
    
    private async Task<List<NetworkDto>> CalculateAvgNetworkFeeListAsync(List<NetworkDto> networkList, string symbol)
    {
        foreach (var network in networkList)
        {
            var avgExchange = await GetAvgExchangeAsync(network.Network, symbol);
            network.WithdrawFee = (network.WithdrawFee.SafeToDecimal() * avgExchange)
                .ToString(ThirdPartDigitals, DecimalHelper.GetDecimals(symbol), DecimalHelper.RoundingOption.Ceiling);
            network.WithdrawFeeUnit = symbol;
        }

        return networkList;
    }

    private async Task<string> GetCacheFeeAsync(string network, string symbol)
    {
        var coBoCoinGrain = _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(network, symbol));
        var coin = await coBoCoinGrain.GetCacheAsync();
        return coin?.AbsEstimateFee;
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
                networkDto.Status = config.DepositInfo.IsOpen
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline;
                multiConfirmSeconds = config.DepositInfo.MultiConfirmSeconds;
            }

            if (type == OrderTypeEnum.Withdraw.ToString() && config.WithdrawInfo != null)
            {
                networkDto.Status = config.WithdrawInfo.IsOpen
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline;
                multiConfirmSeconds = config.WithdrawInfo.MultiConfirmSeconds;
            }

            networkDto.MultiConfirmTime = TimeHelper.SecondsToMinute((int)multiConfirmSeconds);
        }
    }
}