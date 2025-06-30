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
using ETransferServer.Token;
using ETransferServer.Token.Dtos;
using NBitcoin;
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
    private readonly CoinGeckoOptions _coinGeckoOptions;
    private readonly IOptionsSnapshot<WithdrawInfoOptions> _withdrawInfoOptions;
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private readonly IOptionsSnapshot<TokenSupportedChainInfoOptions> _tokenSupportedChainOptions;
    private readonly ITokenNetworkProvider _tokenNetworkProvider;
    private readonly ISupportedChainTokenProvider _supportedChainTokenProvider;
    private readonly IOptionsSnapshot<NetworkInfoOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<ServiceFeeOptions> _serviceFeeOptions;

    public NetworkAppService(ILogger<NetworkAppService> logger, 
        IOptionsSnapshot<CoinGeckoOptions> coinGeckoOptions,
        IObjectMapper objectMapper,
        IClusterClient clusterClient, 
        IOptionsSnapshot<WithdrawInfoOptions> withdrawInfoOptions,
        IOptionsSnapshot<TokenInfoOptions> tokenOptions, IOptionsSnapshot<TokenSupportedChainInfoOptions> tokenSupportedChainOptions, ITokenNetworkProvider tokenNetworkProvider, ISupportedChainTokenProvider supportedChainTokenProvider, IOptionsSnapshot<NetworkInfoOptions> networkInfoOptions, IOptionsSnapshot<ServiceFeeOptions> serviceFeeOptions)
    {
        _logger = logger;
        _coinGeckoOptions = coinGeckoOptions.Value;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
        _withdrawInfoOptions = withdrawInfoOptions;
        _tokenOptions = tokenOptions;
        _tokenSupportedChainOptions = tokenSupportedChainOptions;
        _tokenNetworkProvider = tokenNetworkProvider;
        _supportedChainTokenProvider = supportedChainTokenProvider;
        _networkInfoOptions = networkInfoOptions;
        _serviceFeeOptions = serviceFeeOptions;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(NetworkAppService), 
        MethodName = nameof(HandleGetNetworkTokenListExceptionAsync))]
    public async Task<GetNetworkTokenListDto> GetNetworkTokenListAsync(GetNetworkTokenListRequestDto request,
        string version = null)
    {
        var getNetworkTokenListDto = new GetNetworkTokenListDto();
        // var symbolList = _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList();
        var symbolList = _tokenNetworkProvider.GetSupportTransferSymbolList();
        foreach (var symbol in symbolList)
        {
            if (!request.TokenList.IsNullOrEmpty() && !request.TokenList.Contains(symbol)) continue;
            // if (!_networkOptions.Value.NetworkMap.ContainsKey(symbol)) continue;
            if (!_tokenSupportedChainOptions.Value.SupportedChains.ContainsKey(symbol))
            {
                continue;
            }
            // var fullNetworkConfigs = _networkOptions.Value.NetworkMap[symbol].Where(a =>
            //     a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
            var fullNetworkConfigs = _tokenSupportedChainOptions.Value.SupportedChains[symbol]
                .Where(a => a.SupportedType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
            var networkConfigs = request.NetworkList.IsNullOrEmpty()
                ? fullNetworkConfigs
                : _tokenSupportedChainOptions.Value.SupportedChains[symbol]
                    .Where(n => request.NetworkList.Contains(n.Network) &&
                                n.SupportedType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
                // : _networkOptions.Value.NetworkMap[symbol].Where(a =>
                //     request.NetworkList.Contains(a.NetworkInfo.Network) &&
                //     a.SupportType.Contains(OrderTypeEnum.Transfer.ToString())).ToList();
            var fullNetworkBasicInfoList = await FilterByVersionAndWhiteList(fullNetworkConfigs, version, request.SourceType, request.Address);
            var networkBasicInfoList = await FilterByVersionAndWhiteList(networkConfigs, version, request.SourceType, request.Address);
            var fullBasicDtos = ConvertNetworkBasic(symbol, fullNetworkBasicInfoList);
            var basicDtos = ConvertNetworkBasic(symbol, networkBasicInfoList);
            
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
                            Network = split[1], Name = GetNetworkName(split[1]),
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

    private List<NetworkBasicDto> ConvertNetworkBasic(string symbol,List<NetworkBasicInfo> networkBasicInfos)
    {
        return networkBasicInfos.Select(full => new NetworkBasicDto
            {
                Name = full.Name,
                Network = full.Network,
                Status = _supportedChainTokenProvider.IsWithdrawHealth(symbol, full.Network)
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline
            })
            .ToList();
    }

    private string GetNetworkName(string network)
    {
        return _tokenNetworkProvider.GetNetworkInfo(network).Name;
    }
    
    private string GetNetworkStatus(string symbol, string network)
    {
        if (network == ChainId.AELF || network == ChainId.tDVV || network == ChainId.tDVW)
            return CommonConstant.NetworkStatus.Health;
        return _supportedChainTokenProvider.IsWithdrawHealth(symbol,network)
            ? CommonConstant.NetworkStatus.Health
            : CommonConstant.NetworkStatus.Offline;
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
        string version = null, bool isAddressSupport = false, string sourceType = null, string address = null)
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
                            && _tokenSupportedChainOptions.Value.SupportedChains.ContainsKey(request.Symbol)),
            "Symbol is not exist. Please refresh and try again.");

        var networkConfigs = new List<SupportedChainInfo>();
        FilterNetworkList(request.Symbol, request.Type, request.ChainId, networkConfigs);
        
        var networkBasicInfos = await FilterByVersionAndWhiteList(networkConfigs, version, sourceType, address);
        
        var getNetworkListDto = new GetNetworkListDto();
        getNetworkListDto.ChainId = request.ChainId;

        getNetworkListDto.NetworkList = _objectMapper.Map<List<NetworkBasicInfo>, List<NetworkDto>>(networkBasicInfos);
        FillMultiConfirmMinutes(request.Type, request.Symbol, getNetworkListDto.NetworkList,
            networkConfigs.Select(n=>_tokenNetworkProvider.GetNetworkInfo(n.Network)).ToList());

        // foreach (var networkDto in getNetworkListDto.NetworkList)
        // {
        //     if (request.Type == OrderTypeEnum.Deposit.ToString()) continue;
        //     networkDto.WithdrawFeeUnit = withdraw.WithdrawLocalFeeUnit;
        //     networkDto.WithdrawFee = withdraw.WithdrawLocalFee.ToString(CultureInfo.InvariantCulture);
        //     networkDto.SpecialWithdrawFeeDisplay = withdraw.SpecialWithdrawFeeDisplay;
        //     networkDto.SpecialWithdrawFee = withdraw.SpecialWithdrawFee;
        // }

        if (request.Address.IsNullOrEmpty() || (isAddressSupport && VerifyHelper.VerifyAelfAddress(request.Address))) 
            return getNetworkListDto;

        var networkByAddress = _networkInfoOptions.Value.NetworkPattern
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

    private void FilterNetworkList(string requestSymbol, string requestType, string requestChainId,
        List<SupportedChainInfo> networkConfigs)
    {
        if (requestSymbol.IsNullOrEmpty())
        {
            var transferSymbols = _supportedChainTokenProvider
                .GetTokenListByType(OrderTypeEnum.Transfer.ToString(), null).Select(t => t.Symbol).ToList();
            
            foreach (var symbol in transferSymbols)
            {
                if (!_tokenSupportedChainOptions.Value.SupportedChains.ContainsKey(symbol))
                    continue;

                var configs = _tokenSupportedChainOptions.Value.SupportedChains[symbol];
                
                var filteredConfigs = configs
                    .Where(a => a.SupportedType != null && a.SupportedType.Contains(requestType))
                    .ToList();

                foreach (var config in filteredConfigs)
                {
                    if (networkConfigs.Any(c => c.Network == config.Network))
                        continue;

                    networkConfigs.Add(config);
                }
            }
        }
        else
        {
            if (_tokenSupportedChainOptions.Value.SupportedChains.TryGetValue(requestSymbol, out var configs))
            {
                foreach (var config in configs)
                {
                    if (config.SupportedType == null || !config.SupportedType.Contains(requestType))
                        continue;
                    
                    if (requestChainId != null && requestType != OrderTypeEnum.Transfer.ToString())
                    {
                        if (config.SupportChain == null || !config.SupportChain.Contains(requestChainId))
                            continue;
                    }

                    networkConfigs.Add(config);
                }
            }
        }
    }

    private Dictionary<string, SupportedChainInfo> WrappedNetworkInfoToDic(string requestSymbol, string requestType, string network)
    {
        var res = new Dictionary<string, SupportedChainInfo>();
        if (requestSymbol.IsNullOrEmpty())
        {
            var transferSymbols = _supportedChainTokenProvider
                .GetTokenListByType(OrderTypeEnum.Transfer.ToString(), null).Select(t => t.Symbol).ToList();
            
            foreach (var symbol in transferSymbols)
            {
                var config = _tokenSupportedChainOptions.Value.SupportedChains[symbol]
                    .FirstOrDefault(n => n.Network == network);
                res.TryAdd(symbol, config);
            }
        }
        else
        {
            if (_tokenSupportedChainOptions.Value.SupportedChains.TryGetValue(requestSymbol, out var configs))
            {
                foreach (var config in configs)
                {
                    if (config.SupportedType.Contains(requestType) && config.Network == network)
                    {
                        res.TryAdd(requestSymbol, config);
                    }
                }
            }
        }

        return res;
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
        return Task.FromResult(_serviceFeeOptions.Value.MinThirdPartFee.TryGetValue(minFeeKey, out var value)
            ? value
            : CommonConstant.DefaultConst.DefaultMinThirdPartFee);
    }

    public Task<decimal> GetMaxThirdPartFeeAsync(string network, string symbol)
    {
        var maxFeeKey = string.Join(CommonConstant.Underline, network, symbol);
        return Task.FromResult(_serviceFeeOptions.Value.MaxThirdPartFee.TryGetValue(maxFeeKey, out var value)
            ? value
            :  0M);
    }

    public async Task<Tuple<bool, decimal, decimal, decimal>> GetServiceFeeAsync(string network, string symbol)
    {
        var isOpen = _serviceFeeOptions.Value.IsOpen;
        var amountThreshold = _serviceFeeOptions.Value.AmountThreshold.ContainsKey(symbol)
            ? _serviceFeeOptions.Value.AmountThreshold[symbol]
            : 0M;
        var serviceFee = 0M;
        if (!network.IsNullOrEmpty())
        {
            var (estimateFee, coin) = network == ChainId.AELF || network == ChainId.tDVV || network == ChainId.tDVW
                ? Tuple.Create(0M, new CoBoCoinDto { ExpireTime = 0L })
                : await CalculateNetworkFeeAsync(network, symbol);
            serviceFee = Math.Min(estimateFee, await GetMaxThirdPartFeeAsync(network, symbol)).ToString(
                2, DecimalHelper.RoundingOption.Ceiling).SafeToDecimal();
        }

        var minAmount = _serviceFeeOptions.Value.MinDeposit.ContainsKey(symbol)
            ? _serviceFeeOptions.Value.MinDeposit[symbol]
            : 0M;
        _logger.LogDebug("Deposit from network fee: {network}, {symbol}, {isOpen}, {threshold}, {serviceFee}, {minAmount}", 
            network, symbol, isOpen, amountThreshold, serviceFee, minAmount);
        return Tuple.Create(isOpen, amountThreshold, serviceFee, minAmount);
    }

    public Task<int> GetDecimalsAsync(string chainId, string symbol)
    {
        var tokenDecimals = _tokenOptions.Value.Tokens[chainId][symbol].Decimal;
        return Task.FromResult(tokenDecimals);
    }

    public Task<string> GetIconAsync(string orderType, string chainId, string fromSymbol, string toSymbol = null)
    {
        var icon = _tokenOptions.Value.Tokens[chainId][fromSymbol].Icon;
        return Task.FromResult(icon);
    }

    [ExceptionHandler(typeof(Exception), 
        TargetType = typeof(NetworkAppService), MethodName = nameof(HandleGetTokenPriceListExceptionAsync))]
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
    
    private async Task<List<NetworkBasicInfo>> FilterByVersionAndWhiteList(List<SupportedChainInfo> supportNetworkList,
        string version = null, string sourceType = null, string address = null)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if (userId.HasValue)
        {
            _logger.LogInformation("GetNetworkList currentUser:{userId},version:{version}", userId.Value, version);
            var userGrain = _clusterClient.GetGrain<IUserGrain>(userId.Value);
            var userDto = await userGrain.GetUser();
            var res = new List<NetworkBasicInfo>();
            if (userDto.Success && userDto.Data != null && !userDto.Data.AddressInfos.IsNullOrEmpty())
            {
                return GetNetworkBasicInfos(supportNetworkList,version,
                    userDto.Data.AddressInfos.Select(a => a.Address).ToList());
            }
        }

        if (!sourceType.IsNullOrEmpty() && !address.IsNullOrEmpty() &&
            Enum.TryParse<WalletEnum>(sourceType, true, out var walletType))
        {
            var fullAddress = (int)walletType > 1 
                ? string.Concat(sourceType.ToLower(), CommonConstant.Underline, address)
                : address;
            return GetNetworkBasicInfos(supportNetworkList, version, new List<string>() { fullAddress });
        }

        return GetNetworkBasicInfos(supportNetworkList, version, null);
    }

    private List<NetworkBasicInfo> GetNetworkBasicInfos(List<SupportedChainInfo> supportNetworkList,string version,List<string> addressInfos)
    {
        var res = new List<NetworkBasicInfo>();
        foreach (var supportedChainInfo in supportNetworkList)
        {
            var network = supportedChainInfo.Network;
            var supportWhiteList = supportedChainInfo.SupportWhiteList;
            var networkInfo = _tokenNetworkProvider.GetNetworkInfo(network);
            if ((networkInfo != null && networkInfo.MinShowVersion.IsNullOrEmpty()) ||
                (VerifyHelper.VerifyMemoVersion(version, networkInfo.MinShowVersion)
                 && (supportWhiteList.IsNullOrEmpty() || (addressInfos != null && 
                     supportWhiteList.Any(t =>addressInfos.Exists(a =>
                         a.ToLower() == t.ToLower()))))))
            {
                res.Add(networkInfo);
            }
        }

        return res;
    }

    private void FillMultiConfirmMinutes(string type, string symbol, List<NetworkDto> networkList, 
        List<NetworkBasicInfo> networkConfigs)
    {
        foreach (var networkDto in networkList)
        {
            var config = networkConfigs
                .FirstOrDefault(c => c.Network == networkDto.Network);
            if (config == null) continue;

            var multiConfirmSeconds = config.MultiConfirmSeconds;
            var multiTokens = WrappedNetworkInfoToDic(symbol, type, networkDto.Network);

            // var multiTokens = symbol.IsNullOrEmpty()
            //     ? _networkOptions.Value.NetworkMap.Where(t => _tokenOptions.Value.Transfer.Any(
            //             c => c.Symbol == t.Key)).OrderBy(m => 
            //             _tokenOptions.Value.Transfer.Select(t => t.Symbol).ToList().IndexOf(m.Key))
            //         .ToDictionary(kv => kv.Key, kv => kv.Value.FirstOrDefault(t => t.NetworkInfo.Network == networkDto.Network))
            //     : _networkOptions.Value.NetworkMap[symbol].Where(a =>
            //             a.SupportType.Contains(type) && a.NetworkInfo.Network == networkDto.Network)
            //         .ToDictionary(c => symbol, c => c);
            if (type == OrderTypeEnum.Deposit.ToString())
            {
                networkDto.Status = _supportedChainTokenProvider.IsDepositHealth(symbol, networkDto.Network)
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline;
                multiConfirmSeconds = config.MultiConfirmSeconds;
                foreach (var kv in multiTokens) {
                    networkDto.MultiStatus ??= new Dictionary<string, string>();
                    if (kv.Value != null)
                    {
                        networkDto.MultiStatus.AddOrReplace(kv.Key, _supportedChainTokenProvider.IsDepositHealth(symbol, kv.Value.Network)
                            ? CommonConstant.NetworkStatus.Health
                            : CommonConstant.NetworkStatus.Offline);
                    }
                }
            }

            if ((type == OrderTypeEnum.Withdraw.ToString() || type == OrderTypeEnum.Transfer.ToString()))
            {
                networkDto.Status = _supportedChainTokenProvider.IsWithdrawHealth(CommonConstant.Symbol.USDT, networkDto.Network)
                    ? CommonConstant.NetworkStatus.Health
                    : CommonConstant.NetworkStatus.Offline;
                multiConfirmSeconds = config.MultiConfirmSeconds;
                foreach (var kv in multiTokens) {
                    networkDto.MultiStatus ??= new Dictionary<string, string>();
                    if (kv.Value != null)
                    {
                        networkDto.MultiStatus.AddOrReplace(kv.Key, _supportedChainTokenProvider.IsWithdrawHealth(symbol, kv.Value.Network)
                            ? CommonConstant.NetworkStatus.Health
                            : CommonConstant.NetworkStatus.Offline);
                    }
                }
            }

            networkDto.MultiConfirmTime = TimeHelper.SecondsToMinute((int)multiConfirmSeconds);
        }
    }
}