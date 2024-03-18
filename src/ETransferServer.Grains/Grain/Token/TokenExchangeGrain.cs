using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.State.Token;
using ETransferServer.Options;
using ETransferServer.ThirdPart.Exchange;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenExchangeGrain : IGrainWithStringKey
{
    public static string GetGrainId(string fromSymbol, string toSymbol)
    {
        return string.Join(CommonConstant.Underline, fromSymbol, toSymbol);
    }

    Task<Dictionary<string, TokenExchangeDto>> GetAsync();
    Task<Dictionary<string, TokenExchangeDto>> GetByProviderAsync(ExchangeProviderName name, string symbol);
}

public class TokenExchangeGrain : Grain<TokenExchangeState>, ITokenExchangeGrain
{
    private readonly ILogger<TokenExchangeGrain> _logger;
    private readonly Dictionary<string, IExchangeProvider> _exchangeProviders;
    private readonly IOptionsMonitor<ExchangeOptions> _exchangeOptions;
    private readonly IOptionsMonitor<NetWorkReflectionOptions> _netWorkReflectionOption;

    public TokenExchangeGrain(IEnumerable<IExchangeProvider> exchangeProviders,
        IOptionsMonitor<ExchangeOptions> exchangeOptions,
        IOptionsMonitor<NetWorkReflectionOptions> netWorkReflectionOption, ILogger<TokenExchangeGrain> logger)
    {
        _exchangeOptions = exchangeOptions;
        _netWorkReflectionOption = netWorkReflectionOption;
        _logger = logger;
        _exchangeProviders = exchangeProviders.ToDictionary(p => p.Name().ToString());
    }


    public async Task<Dictionary<string, TokenExchangeDto>> DoGetAsync(string fromSymbol, string toSymbol)
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
                    Timestamp = now
                }
            };
        }

        var asyncTasks = new Dictionary<string, Task<TokenExchangeDto>>();
        var isUsd = toSymbol.ToUpper() == CommonConstant.Symbol.USD;
        toSymbol = isUsd ? CommonConstant.Symbol.USDT : toSymbol;
        var usdToUsdtTask = isUsd ? ExchangeFromUsdtToUsd() : null;
        var providerOption = _exchangeOptions.CurrentValue.GetSymbolProviders(fromSymbol, toSymbol);
        var providers = _exchangeProviders.Values.Where(provider => providerOption.Contains(provider.Name().ToString()))
            .ToList();
        foreach (var provider in providers)
        {
            asyncTasks[provider.Name().ToString()] = provider.LatestAsync(fromSymbol, toSymbol);
        }

        var result = new Dictionary<string, TokenExchangeDto>();
        var usdtPriceInUsd = isUsd ? await usdToUsdtTask : null;
        foreach (var (providerName, exchangeTask) in asyncTasks)
        {
            try
            {
                var exchange = await exchangeTask;

                // if usd, convert price to usd
                exchange.Exchange = isUsd ? exchange.Exchange * usdtPriceInUsd.Exchange : exchange.Exchange;

                result.Add(providerName, exchange);
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
        if (!_exchangeOptions.CurrentValue.BottomExchange.TryGetValue(symbolPair, out var bottomExchange))
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
            Timestamp = now
        });
        return result;
    }

    public async Task<Dictionary<string, TokenExchangeDto>> GetViaUsdt(string fromSymbol, string toSymbol)
    {
        var fromToUsdtTask = DoGetAsync(fromSymbol, CommonConstant.Symbol.USDT);
        var toToUsdtTask = DoGetAsync(toSymbol, CommonConstant.Symbol.USDT);

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
            _exchangeOptions.CurrentValue.SymbolExchangeViaUSDT.Contains(fromSymbol) ||
            _exchangeOptions.CurrentValue.SymbolExchangeViaUSDT.Contains(toSymbol)
                ? await GetViaUsdt(fromSymbol, toSymbol)
                : await DoGetAsync(fromSymbol, toSymbol);

        State.LastModifyTime = now;
        State.ExpireTime = now + _exchangeOptions.CurrentValue.DataExpireSeconds * 1000;
        await WriteStateAsync();
        return State.ExchangeInfos;
    }


    private async Task<TokenExchangeDto> ExchangeFromUsdtToUsd()
    {
        try
        {
            AssertHelper.IsTrue(_exchangeProviders.ContainsKey(ExchangeProviderName.CoinGecko.ToString()),
                "CoinGecko not support");
            var resp = await _exchangeProviders.GetValueOrDefault(ExchangeProviderName.CoinGecko.ToString())
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
                Timestamp = DateTime.UtcNow.ToUtcMilliSeconds(),
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
        State.ExpireTime = now + _exchangeOptions.CurrentValue.DataExpireSeconds * 1000;
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
        return _netWorkReflectionOption.CurrentValue.SymbolItems.TryGetValue(sourceSymbol, out var targetSymbol)
            ? targetSymbol
            : sourceSymbol;
    }
}