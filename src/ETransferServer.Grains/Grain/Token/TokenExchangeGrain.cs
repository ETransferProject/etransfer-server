using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.State.Token;
using ETransferServer.Options;
using ETransferServer.ThirdPart.Exchange;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenExchangeGrain : IGrainWithStringKey
{
    public static string GetGrainId(string fromSymbol, string toSymbol)
    {
        return string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
    }

    Task<Dictionary<string, TokenExchangeDto>> GetAsync();
    Task<Dictionary<string, TokenExchangeDto>> GetHistoryAsync();
    Task<Dictionary<string, TokenExchangeDto>> GetByProviderAsync(ExchangeProviderName name, string symbol);
}

public class TokenExchangeGrain : Grain<TokenExchangeState>, ITokenExchangeGrain
{
    private readonly ILogger<TokenExchangeGrain> _logger;
    private readonly Dictionary<string, IExchangeProvider> _exchangeProviders;
    private readonly IOptionsSnapshot<ExchangeOptions> _exchangeOptions;
    private readonly IOptionsSnapshot<NetWorkReflectionOptions> _netWorkReflectionOption;

    public TokenExchangeGrain(IEnumerable<IExchangeProvider> exchangeProviders,
        IOptionsSnapshot<ExchangeOptions> exchangeOptions,
        IOptionsSnapshot<NetWorkReflectionOptions> netWorkReflectionOption, ILogger<TokenExchangeGrain> logger)
    {
        _exchangeOptions = exchangeOptions;
        _netWorkReflectionOption = netWorkReflectionOption;
        _logger = logger;
        _exchangeProviders = exchangeProviders.ToDictionary(p => p.Name().ToString());
    }


    public async Task<Dictionary<string, TokenExchangeDto>> DoGetAsync(string fromSymbol, string toSymbol, long timestamp = 0L)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (fromSymbol == toSymbol)
        {
            return new Dictionary<string, TokenExchangeDto>
            {
                ["default"] = new()
                {
                    FromSymbol = fromSymbol,
                    ToSymbol = toSymbol,
                    Exchange = 1,
                    Timestamp = timestamp > 0 ? timestamp : now
                }
            };
        }

        var asyncTasks = new Dictionary<string, Task<TokenExchangeDto>>();
        var isUsd = toSymbol.ToUpper() == CommonConstant.Symbol.USD ? "to" :
            fromSymbol.ToUpper() == CommonConstant.Symbol.USD ? "from" : "none";
        toSymbol = isUsd == "to" ? CommonConstant.Symbol.USDT : toSymbol;
        fromSymbol = isUsd == "from" ? CommonConstant.Symbol.USDT : fromSymbol;
        if (isUsd == "from")
        {
            (fromSymbol, toSymbol) = (toSymbol, fromSymbol);
        }

        var usdToUsdtTask = isUsd != "none" ? ExchangeFromUsdtToUsd(timestamp) : null;
        var providerOption = _exchangeOptions.Value.GetSymbolProviders(fromSymbol, toSymbol);
        var providers = _exchangeProviders.Values.Where(provider => providerOption.Contains(provider.Name().ToString()))
            .ToList();
        foreach (var provider in providers)
        {
            asyncTasks[provider.Name().ToString()] = timestamp > 0
                ? provider.HistoryAsync(fromSymbol, toSymbol, timestamp)
                : provider.LatestAsync(fromSymbol, toSymbol);
        }

        var result = new Dictionary<string, TokenExchangeDto>();
        var usdtPriceInUsd = isUsd != "none" ? await usdToUsdtTask : null;
        foreach (var (providerName, exchangeTask) in asyncTasks)
        {
            try
            {
                var exchange = await exchangeTask;

                // if usd, convert price to usd
                exchange.Exchange = isUsd == "to" ? exchange.Exchange * usdtPriceInUsd.Exchange :
                    isUsd == "from" ? 1 / (exchange.Exchange * usdtPriceInUsd.Exchange) : exchange.Exchange;

                result.Add(providerName, exchange);
                _logger.LogInformation(
                    "Token exchange: fromSymbol={fromSymbol}, toSymbol={toSymbol}, timestamp={timestamp}, name={providerName}, exchange={exchange}, usdExchange={usdExchange}",
                    fromSymbol, toSymbol, timestamp, providerName, exchange.Exchange, usdtPriceInUsd?.Exchange);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Query exchange failed, providerName={ProviderName}", providerName);
            }
        }

        if (!result.IsNullOrEmpty())
        {
            return result;
        }

        var symbolPair = string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
        _logger.LogWarning("Exchange empty, use bottom exchange {Pair}", symbolPair);
        if (!_exchangeOptions.Value.BottomExchange.TryGetValue(symbolPair, out var bottomExchange))
        {
            return result;
        }

        _logger.LogWarning("Exchange empty, use bottom exchange {Pair}, price={Price}", symbolPair,
            bottomExchange);
        result.Add("bottom", new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Exchange = bottomExchange.SafeToDecimal(),
            Timestamp = timestamp > 0 ? timestamp : now
        });
        return result;
    }

    public async Task<Dictionary<string, TokenExchangeDto>> GetViaUsdt(string fromSymbol, string toSymbol, long timestamp = 0L)
    {
        var fromToUsdtTask = DoGetAsync(fromSymbol, CommonConstant.Symbol.USDT, timestamp);
        var toToUsdtTask = DoGetAsync(toSymbol, CommonConstant.Symbol.USDT, timestamp);

        var fromToUsdt = await fromToUsdtTask;
        var toToUsdt = await toToUsdtTask;
        AssertHelper.NotEmpty(fromToUsdt, "GetViaUSDT failed, from={}, to={}, fromSymbolUsdtEmpty", fromSymbol,
            toSymbol);
        AssertHelper.NotEmpty(toToUsdt, "GetViaUSDT failed, from={}, to={}, toSymbolUsdtEmpty", fromSymbol, toSymbol);


        return new Dictionary<string, TokenExchangeDto>
        {
            ["viaUSDT"] = new()
            {
                FromSymbol = fromSymbol,
                ToSymbol = toSymbol,
                Timestamp = DateTime.UtcNow.ToUtcMilliSeconds(),
                Exchange = fromToUsdt.Values.Select(ex => ex.Exchange).Average() /
                           toToUsdt.Values.Select(ex => ex.Exchange).Average()
            }
        };
    }

    public async Task<Dictionary<string, TokenExchangeDto>> GetAsync()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.LastModifyTime > 0 && State.ExpireTime > now)
        {
            return State.ExchangeInfos;
        }

        var symbolValue = this.GetPrimaryKeyString().Split(CommonConstant.Underline);
        if (symbolValue.Length < 2)
        {
            return new Dictionary<string, TokenExchangeDto>();
        }

        var fromSymbol = MappingSymbol(symbolValue[0].ToUpper());
        var toSymbol = MappingSymbol(symbolValue[1].ToUpper());

        State.ExchangeInfos =
            _exchangeOptions.Value.SymbolExchangeViaUSDT.Contains(fromSymbol) ||
            _exchangeOptions.Value.SymbolExchangeViaUSDT.Contains(toSymbol)
                ? await GetViaUsdt(fromSymbol, toSymbol)
                : await DoGetAsync(fromSymbol, toSymbol);

        State.LastModifyTime = now;
        State.ExpireTime = now + _exchangeOptions.Value.DataExpireSeconds * 1000;
        await WriteStateAsync();
        return State.ExchangeInfos;
    }
    
    public async Task<Dictionary<string, TokenExchangeDto>> GetHistoryAsync()
    {
        if (State.LastModifyTime > 0)
        {
            return State.ExchangeInfos;
        }

        var symbolValue = this.GetPrimaryKeyString().Split(CommonConstant.Underline);
        if (symbolValue.Length < 3)
        {
            return new Dictionary<string, TokenExchangeDto>();
        }

        var fromSymbol = MappingSymbol(symbolValue[0].ToUpper());
        var toSymbol = MappingSymbol(symbolValue[1].ToUpper());
        var timestamp = long.Parse(symbolValue[2]);

        State.ExchangeInfos =
            _exchangeOptions.Value.SymbolExchangeViaUSDT.Contains(fromSymbol) ||
            _exchangeOptions.Value.SymbolExchangeViaUSDT.Contains(toSymbol)
                ? await GetViaUsdt(fromSymbol, toSymbol, timestamp)
                : await DoGetAsync(fromSymbol, toSymbol, timestamp);

        State.LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds();
        await WriteStateAsync();
        return State.ExchangeInfos;
    }


    private async Task<TokenExchangeDto> ExchangeFromUsdtToUsd(long timestamp)
    {
        try
        {
            AssertHelper.IsTrue(_exchangeProviders.ContainsKey(ExchangeProviderName.CoinGecko.ToString()),
                "CoinGecko not support");
            var resp = timestamp > 0
                ? await _exchangeProviders.GetValueOrDefault(ExchangeProviderName.CoinGecko.ToString())
                    .HistoryAsync(CommonConstant.Symbol.USDT, CommonConstant.Symbol.USD, timestamp)
                : await _exchangeProviders.GetValueOrDefault(ExchangeProviderName.CoinGecko.ToString())
                    .LatestAsync(CommonConstant.Symbol.USDT, CommonConstant.Symbol.USD);
            return resp;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get usdt price in usd failed, use the fixed price");
            return new TokenExchangeDto
            {
                FromSymbol = CommonConstant.Symbol.USD,
                ToSymbol = CommonConstant.Symbol.USDT,
                Timestamp = timestamp > 0 ? timestamp : DateTime.UtcNow.ToUtcMilliSeconds(),
                Exchange = 1M
            };
        }
    }


    public async Task<Dictionary<string, TokenExchangeDto>> GetByProviderAsync(ExchangeProviderName name, string symbol)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.LastModifyTime > 0 && State.ExpireTime > now)
        {
            return State.ExchangeInfos;
        }

        var symbolValue = this.GetPrimaryKeyString().Split(CommonConstant.Underline).ToList();
        if (symbolValue.Count < 1)
        {
            return new Dictionary<string, TokenExchangeDto>();
        }

        symbolValue = symbolValue.ConvertAll(item => MappingSymbol(item));
        var asyncTasks = await _exchangeProviders[name.ToString()].LatestAsync(symbolValue, symbol);

        State.LastModifyTime = now;
        State.ExpireTime = now + _exchangeOptions.Value.DataExpireSeconds * 1000;
        State.ExchangeInfos = new Dictionary<string, TokenExchangeDto>();
        foreach (var item in asyncTasks)
        {
            try
            {
                State.ExchangeInfos.Add(item.FromSymbol, item);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Query exchange failed, token={tokenName}, providerName={providerName}",
                    item.FromSymbol, name);
            }
        }

        await WriteStateAsync();

        return State.ExchangeInfos;
    }

    private string MappingSymbol(string sourceSymbol)
    {
        return _netWorkReflectionOption.Value.SymbolItems.TryGetValue(sourceSymbol, out var targetSymbol)
            ? targetSymbol
            : sourceSymbol;
    }
}