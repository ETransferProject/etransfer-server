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
    Task<Dictionary<string, TokenExchangeDto>> GetAsync();
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
        foreach (var provider in _exchangeProviders.Values)
        {
            asyncTasks[provider.Name().ToString()] =
                provider.LatestAsync(MappingSymbol(symbolValue[0].ToUpper()), MappingSymbol(symbolValue[1].ToUpper()));
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

    private string MappingSymbol(string sourceSymbol)
    {
        return _netWorkReflectionOption.CurrentValue.SymbolItems.TryGetValue(sourceSymbol, out var targetSymbol)
            ? targetSymbol
            : sourceSymbol;
    }
    
}