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

        var asyncTasks = new Dictionary<string, Task<TokenExchangeDto>>();
        var fromSymbol = MappingSymbol(symbolValue[0].ToUpper());
        var toSymbol = MappingSymbol(symbolValue[1].ToUpper());
        var providerOption =
            _exchangeOptions.CurrentValue.SymbolProviders.GetValueOrDefault(symbolValue[1].ToUpper(),
                _exchangeOptions.CurrentValue.DefaultProviders);
        var providers = _exchangeProviders.Values.Where(provider => providerOption.Contains(provider.Name().ToString()))
            .ToList();
        foreach (var provider in providers)
        {
            asyncTasks[provider.Name().ToString()] = provider.LatestAsync(fromSymbol, toSymbol);
        }

        State.LastModifyTime = now;
        State.ExpireTime = now + _exchangeOptions.CurrentValue.DataExpireSeconds * 1000;
        State.ExchangeInfos = new Dictionary<string, TokenExchangeDto>();
        foreach (var (providerName, exchangeTask) in asyncTasks)
        {
            try
            {
                State.ExchangeInfos.Add(providerName, await exchangeTask);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Query exchange failed, providerName={ProviderName}", providerName);
            }
        }

        await WriteStateAsync();

        return State.ExchangeInfos;
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