using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.Network.Dtos;
using ETransferServer.ThirdPart.Exchange;
using ETransferServer.Token.Dtos;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.Network;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public partial class NetworkAppService : ETransferServerAppService, INetworkAppService
{
    private readonly ILogger<NetworkAppService> _logger;
    private readonly IOptionsSnapshot<NetworkOptions> _networkOptions;
    private readonly CoinGeckoOptions _coinGeckoOptions;
    private readonly IOptionsSnapshot<WithdrawInfoOptions> _withdrawInfoOptions;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;

    public NetworkAppService(ILogger<NetworkAppService> logger, 
        IOptionsSnapshot<NetworkOptions> networkOptions,
        IOptionsSnapshot<CoinGeckoOptions> coinGeckoOptions,
        IObjectMapper objectMapper,
        IClusterClient clusterClient, 
        IOptionsSnapshot<WithdrawInfoOptions> withdrawInfoOptions,
        IOptionsSnapshot<TokenOptions> tokenOptions)
    {
        _logger = logger;
        _networkOptions = networkOptions;
        _coinGeckoOptions = coinGeckoOptions.Value;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
        _withdrawInfoOptions = withdrawInfoOptions;
        _tokenOptions = tokenOptions;
    }

    [ExceptionHandler(typeof(UserFriendlyException), typeof(Exception), 
        TargetType = typeof(NetworkAppService), MethodName = nameof(HandleGetListExceptionAsync))]
    public async Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request, string version = null)
    {
        var getNetworkListDto = await GetNetworkListWithLocalFeeAsync(request, version);
        if (request.Type == OrderTypeEnum.Deposit.ToString()) return getNetworkListDto;

        // fill withdraw fee
        foreach (var networkDto in getNetworkListDto.NetworkList)
        {
            networkDto.WithdrawFee = await GetCacheFeeAsync(networkDto.Network, request.Symbol) ??
                                     networkDto.WithdrawFee;
        }

        try
        {
            if (!request.Symbol.IsNullOrEmpty())
            {
                getNetworkListDto.NetworkList = request.Symbol == CommonConstant.Symbol.USDT
                    ? await CalculateNetworkFeeListAsync(getNetworkListDto.NetworkList, request.ChainId, request.Symbol)
                    : await CalculateAvgNetworkFeeListAsync(getNetworkListDto.NetworkList, request.ChainId,
                        request.Symbol);
            }
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
        
        getNetworkListDto = FilterByChainId(getNetworkListDto, request.ChainId);

        return getNetworkListDto;
    }

    public async Task<GetNetworkTokenListDto> GetNetworkTokenListAsync(GetNetworkTokenListRequestDto request,
        string version = null)
    {
        var getNetworkTokenListDto = new GetNetworkTokenListDto();
        var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        foreach (var symbol in symbolList)
        {
            if (!request.TokenList.IsNullOrEmpty() && !request.TokenList.Contains(symbol)) continue;
            if (!_networkOptions.Value.NetworkMap.ContainsKey(symbol)) continue;
            var fullNetworkConfigs = _networkOptions.Value.NetworkMap[symbol].Where(a =>
                a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
            var networkConfigs = request.NetworkList.IsNullOrEmpty()
                ? fullNetworkConfigs
                : _networkOptions.Value.NetworkMap[symbol].Where(a =>
                    request.NetworkList.Contains(a.NetworkInfo.Network) &&
                    a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
            fullNetworkConfigs = await FilterByVersionAndWhiteList(fullNetworkConfigs, version, request.SourceType, request.Address);
            networkConfigs = await FilterByVersionAndWhiteList(networkConfigs, version, request.SourceType, request.Address);
            var fullBasicDtos = fullNetworkConfigs.ConvertAll(t => new NetworkBasicDto
            {
                Network = t.NetworkInfo.Network, Name = t.NetworkInfo.Name, Status = t.WithdrawInfo.IsOpen
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline
            });
            var basicDtos = networkConfigs.ConvertAll(t => new NetworkBasicDto
            {
                Network = t.NetworkInfo.Network, Name = t.NetworkInfo.Name, Status = t.WithdrawInfo.IsOpen
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline
            });
            
            if (_withdrawInfoOptions.Value.TransferPath.ContainsKey(symbol))
            {
                var pathList = _withdrawInfoOptions.Value.TransferPath[symbol];
                foreach (var path in pathList)
                {
                    var split = path.Split(CommonConstant.Comma);
                    if (!basicDtos.Exists(t => t.Network == split[0] || t.Network == split[1])) continue;
                    var result = new List<NetworkBasicDto>
                    {
                        new()
                        {
                            Network = split[1], Name = GetNetworkName(symbol, split[1]),
                            Status = GetNetworkStatus(symbol, split[1])
                        }
                    };
                    if (!getNetworkTokenListDto.ContainsKey(split[0]))
                        getNetworkTokenListDto.Add(split[0], new NetworkTokenListDto { [symbol] = result });
                    else
                    {
                        var networkTokenListDto = getNetworkTokenListDto[split[0]];
                        if (!networkTokenListDto.ContainsKey(symbol))
                            networkTokenListDto.Add(symbol, result);
                        else
                        {
                            var basicList = networkTokenListDto[symbol] ?? new List<NetworkBasicDto>();
                            basicList.AddRange(result);
                            networkTokenListDto[symbol] = basicList;
                        }
                    }
                }
                continue;
            }
            
            foreach (var item in basicDtos)
            {
                var result = fullBasicDtos.Where(t => t.Network != item.Network).ToList();
                if (!getNetworkTokenListDto.ContainsKey(item.Network))
                    getNetworkTokenListDto.Add(item.Network, new NetworkTokenListDto { [symbol] = result });
                else
                {
                    var networkTokenListDto = getNetworkTokenListDto[item.Network];
                    if (!networkTokenListDto.ContainsKey(symbol))
                        networkTokenListDto.Add(symbol, result);
                    else
                    {
                        var basicList = networkTokenListDto[symbol] ?? new List<NetworkBasicDto>();
                        basicList.AddRange(result);
                        networkTokenListDto[symbol] = basicList;
                    }
                }
            }
        }
        
        return getNetworkTokenListDto;
    }

    private string GetNetworkName(string symbol, string network)
    {
        var name = _networkOptions.Value.NetworkMap[symbol].
            FirstOrDefault(t => t.NetworkInfo.Network == network)?.NetworkInfo?.Name;
        if (!name.IsNullOrEmpty()) return name;
        return _networkOptions.Value.NetworkMap[CommonConstant.Symbol.USDT]
            .FirstOrDefault(t => t.NetworkInfo.Network == network)
            ?.NetworkInfo?.Name;
    }
    
    private string GetNetworkStatus(string symbol, string network)
    {
        if (network == ChainId.AELF || network == ChainId.tDVV || network == ChainId.tDVW)
            return CommonConstant.NetworkStatus.Health;
        var networkConfig = _networkOptions.Value.NetworkMap[symbol]
            .FirstOrDefault(t => t.NetworkInfo.Network == network);
        if (networkConfig != null) return networkConfig.WithdrawInfo.IsOpen
            ? CommonConstant.NetworkStatus.Health
            : CommonConstant.NetworkStatus.Offline;
        return CommonConstant.NetworkStatus.Health;
    }

    private GetNetworkListDto FilterByChainId(GetNetworkListDto networkListDto, string chainId)
    {
        if (networkListDto.NetworkList.Any())
        {
            networkListDto = new GetNetworkListDto
            {
                ChainId = chainId,
                NetworkList = networkListDto.NetworkList
                    .Where(networkDto => !networkDto.Network.Equals(chainId))
                    .ToList()
            };
        }

        return networkListDto;
    }

    public async Task<GetNetworkListDto> GetNetworkListWithLocalFeeAsync(GetNetworkListRequestDto request, 
        string version = null, bool isAddressSupport = false)
    {
        AssertHelper.NotNull(request, "Request empty. Please refresh and try again.");
        AssertHelper.NotEmpty(request.Type, "Invalid type. Please refresh and try again.");
        if (request.Type == OrderTypeEnum.Deposit.ToString() || request.Type == OrderTypeEnum.Withdraw.ToString())
        {
            AssertHelper.NotEmpty(request.Symbol, "Invalid symbol. Please refresh and try again.");
            AssertHelper.NotEmpty(request.ChainId, "Invalid chainId. Please refresh and try again.");
        }
        AssertHelper.IsTrue(request.Type == OrderTypeEnum.Deposit.ToString()
                            || request.Type == OrderTypeEnum.Withdraw.ToString()
                            || request.Type == OrderTypeEnum.Transfer.ToString(),
            "Invalid type value. Please refresh and try again.");
        AssertHelper.IsTrue(request.Symbol.IsNullOrEmpty() || (!request.Symbol.IsNullOrEmpty() 
                            && _networkOptions.Value.NetworkMap.ContainsKey(request.Symbol)),
            "Symbol is not exist. Please refresh and try again.");

        var networkConfigs = request.Symbol.IsNullOrEmpty()
            ? _networkOptions.Value.NetworkMap.Where(t => _tokenOptions.Value.Transfer.Any(
                    c => c.Symbol == t.Key)).OrderBy(m => 
                    _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList().IndexOf(m.Key))
                .SelectMany(kvp => kvp.Value).Where(a =>
                    a.SupportType.Contains(request.Type)).GroupBy(g => g.NetworkInfo.Network)
                .Select(s => s.First()).ToList()
            : _networkOptions.Value.NetworkMap[request.Symbol].Where(a =>
                    request.Type == OrderTypeEnum.Transfer.ToString() 
                        ? a.SupportType.Contains(request.Type) 
                        : a.SupportType.Contains(request.Type) && a.SupportChain.Contains(request.ChainId))
                .ToList();
        networkConfigs = await FilterByVersionAndWhiteList(networkConfigs, version);

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
            networkDto.SpecialWithdrawFeeDisplay = withdraw.SpecialWithdrawFeeDisplay;
            networkDto.SpecialWithdrawFee = withdraw.SpecialWithdrawFee;
        }

        if (request.Address.IsNullOrEmpty() || (isAddressSupport && VerifyHelper.VerifyAelfAddress(request.Address))) 
            return getNetworkListDto;

        var networkByAddress = _networkOptions.Value.NetworkPattern
            .Where(kv => request.Address.Match(kv.Key))
            .SelectMany(kv => kv.Value)
            .ToList();
        if (!VerifyHelper.VerifyAelfAddress(request.Address))
            networkByAddress.RemoveAll(a => a == ChainId.AELF || a == ChainId.tDVV || a == ChainId.tDVW);
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
        var coin = await coBoCoinGrain.Get();
        AssertHelper.NotNull(coin, "CoBo coin detail not found");
        _logger.LogDebug("CoBo AbsEstimateFee={Fee}, FeeCoin={Coin}, expireTime={Ts}", coin.AbsEstimateFee,
            coin.FeeCoin, coin.ExpireTime);
        var feeCoin = coin.FeeCoin.Split(CommonConstant.Underline);
        var feeSymbol = feeCoin.Length == 1 ? feeCoin[0] : feeCoin[1];

        var avgExchange = await GetAvgExchangeAsync(feeSymbol, symbol);
        var estimateFee = coin.AbsEstimateFee.SafeToDecimal() * avgExchange;
        return Tuple.Create(estimateFee, coin);
    }

    public async Task<decimal> GetAvgExchangeAsync(string fromSymbol, string toSymbol, long timestamp = 0L)
    {
        var exchangeSymbolPair = timestamp > 0 
        ? string.Join(CommonConstant.Underline, fromSymbol, toSymbol, timestamp)
        : string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
        var exchangeGrain = _clusterClient.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
        var exchange = timestamp > 0 
            ? await exchangeGrain.GetHistoryAsync() 
            : await exchangeGrain.GetAsync();
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

    public Task<decimal> GetMinThirdPartFeeAsync(string network, string symbol)
    {
        var minFeeKey = string.Join(CommonConstant.Underline, network, symbol);
        return Task.FromResult(_withdrawInfoOptions.Value.MinThirdPartFee.ContainsKey(minFeeKey)
            ? _withdrawInfoOptions.Value.MinThirdPartFee[minFeeKey]
            : CommonConstant.DefaultConst.DefaultMinThirdPartFee);
    }

    public Task<decimal> GetMaxThirdPartFeeAsync(string network, string symbol)
    {
        var maxFeeKey = string.Join(CommonConstant.Underline, network, symbol);
        return Task.FromResult(_withdrawInfoOptions.Value.MaxThirdPartFee.ContainsKey(maxFeeKey)
            ? _withdrawInfoOptions.Value.MaxThirdPartFee[maxFeeKey]
            : 0M);
    }

    public Task<int> GetDecimalsAsync(string chainId, string symbol)
    {
        return Task.FromResult((!chainId.IsNullOrEmpty() && _tokenOptions.Value.Withdraw.ContainsKey(chainId)
                ? _tokenOptions.Value.Withdraw[chainId]
                : null)
            ?.FirstOrDefault(t => t.Symbol == symbol)
            ?.Decimals ?? DecimalHelper.GetDecimals(symbol));
    }

    public Task<string> GetIconAsync(string orderType, string chainId, string fromSymbol, string toSymbol = null)
    {
        var tokenDic = orderType == OrderTypeEnum.Withdraw.ToString()
            ? _tokenOptions.Value.Withdraw
            : _tokenOptions.Value.Deposit;
        return Task.FromResult(tokenDic.ContainsKey(chainId) && (toSymbol.IsNullOrEmpty() || fromSymbol == toSymbol)
            ? tokenDic[chainId]?.FirstOrDefault(t => t.Symbol == fromSymbol)?.Icon
            : _tokenOptions.Value.DepositSwap.FirstOrDefault(config => config.Symbol == fromSymbol)?.ToTokenList?
                .FirstOrDefault(token => token.Symbol == toSymbol)?.Icon);
    }

    public async Task<ListResultDto<TokenPriceDataDto>> GetTokenPriceListAsync(GetTokenPriceListRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbols)) return new ListResultDto<TokenPriceDataDto>();
        var list = new List<TokenPriceDataDto>();
        var symbols = request.Symbols.Split(CommonConstant.Comma, StringSplitOptions.TrimEntries).Distinct().ToList();
        foreach (var symbol in symbols)
        {
            try
            {
                if (symbol.IsNullOrWhiteSpace()) continue;
                list.Add(new TokenPriceDataDto
                {
                    Symbol = symbol,
                    PriceUsd = await GetAvgExchangeAsync(symbol, CommonConstant.Symbol.USD)
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetTokenPriceListAsync error, {symbol}", symbol);
                list.Add(new TokenPriceDataDto
                {
                    Symbol = symbol,
                    PriceUsd = 0M
                });
            }
        }

        return new ListResultDto<TokenPriceDataDto>(list);
    }

    private async Task<List<NetworkDto>> CalculateNetworkFeeListAsync(List<NetworkDto> networkList, string chainId, string symbol)
    {
        var exchangeSymbolPair = networkList.ConvertAll(item => item.Network).ToList().JoinAsString(CommonConstant.Underline);
        var exchangeGrain = _clusterClient.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
        var exchange = await exchangeGrain.GetByProviderAsync(ExchangeProviderName.CoinGecko, symbol);
        AssertHelper.NotEmpty(exchange, "Exchange data list not found {}", exchangeSymbolPair);

        foreach (var network in networkList)
        {
            if (network.SpecialWithdrawFeeDisplay)
            {
                network.WithdrawFee = network.SpecialWithdrawFee;
            }
            else
            {
                network.WithdrawFee = Math.Max(await GetMinThirdPartFeeAsync(network.Network, symbol),
                        network.WithdrawFee.SafeToDecimal() *
                        exchange[_coinGeckoOptions.CoinIdMapping[network.WithdrawFeeUnit]].Exchange)
                    .ToString(CommonConstant.DefaultConst.ThirdPartDigitals, await GetDecimalsAsync(chainId, symbol),
                        DecimalHelper.RoundingOption.Ceiling);
            }

            network.WithdrawFeeUnit = symbol;
        }

        return networkList;
    }
    

    private async Task<List<NetworkDto>> CalculateAvgNetworkFeeListAsync(List<NetworkDto> networkList, string chainId, string symbol)
    {
        foreach (var network in networkList)
        {
            if (network.SpecialWithdrawFeeDisplay)
            {
                network.WithdrawFee = network.SpecialWithdrawFee;
            }
            else
            {
                var avgExchange = await GetAvgExchangeAsync(network.Network, symbol);

                network.WithdrawFee = Math.Max(await GetMinThirdPartFeeAsync(network.Network, symbol),
                        network.WithdrawFee.SafeToDecimal() * avgExchange)
                    .ToString(CommonConstant.DefaultConst.ThirdPartDigitals, await GetDecimalsAsync(chainId, symbol),
                        DecimalHelper.RoundingOption.Ceiling);
            }

            network.WithdrawFeeUnit = symbol;
        }

        return networkList;
    }

    private async Task<string> GetCacheFeeAsync(string network, string symbol)
    {
        if (symbol.IsNullOrEmpty()) return null;
        var coBoCoinGrain = _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(network, symbol));
        var coin = await coBoCoinGrain.GetCache();
        return coin?.AbsEstimateFee;
    }
    
    private async Task<List<NetworkConfig>> FilterByVersionAndWhiteList(List<NetworkConfig> networkConfigs, 
        string version = null, string sourceType = null, string address = null)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (userId.HasValue)
        {
            _logger.LogInformation("GetNetworkList currentUser:{userId},version:{version}", userId.Value, version);
            var userGrain = _clusterClient.GetGrain<IUserGrain>(userId.Value);
            var userDto = await userGrain.GetUser();
            if (userDto.Success && userDto.Data != null && !userDto.Data.AddressInfos.IsNullOrEmpty())
            {
                return networkConfigs.Where(config =>
                    config.NetworkInfo.MinShowVersion.IsNullOrEmpty()
                    || (VerifyHelper.VerifyMemoVersion(version, config.NetworkInfo.MinShowVersion)
                        && (config.SupportWhiteList.IsNullOrEmpty() ||
                            config.SupportWhiteList.Any(t => userDto.Data.AddressInfos.Exists(a =>
                                a.Address.ToLower() == t.ToLower()))))).ToList();
            }
        }

        if (!sourceType.IsNullOrEmpty() && !address.IsNullOrEmpty() &&
            Enum.TryParse<WalletEnum>(sourceType, true, out _))
        {
            var fullAddress = string.Concat(sourceType.ToLower(), CommonConstant.Underline, address);
            return networkConfigs.Where(config =>
                config.NetworkInfo.MinShowVersion.IsNullOrEmpty()
                || (VerifyHelper.VerifyMemoVersion(version, config.NetworkInfo.MinShowVersion)
                    && (config.SupportWhiteList.IsNullOrEmpty() ||
                        config.SupportWhiteList.Any(t => fullAddress.ToLower() == t.ToLower())))).ToList();
        }

        return networkConfigs.Where(config =>
            config.NetworkInfo.MinShowVersion.IsNullOrEmpty()
            || (VerifyHelper.VerifyMemoVersion(version, config.NetworkInfo.MinShowVersion)
                && config.SupportWhiteList.IsNullOrEmpty())).ToList();
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

            if ((type == OrderTypeEnum.Withdraw.ToString() || type == OrderTypeEnum.Transfer.ToString())
                && config.WithdrawInfo != null)
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