using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CoinGecko.Entities.Response.Simple;
using ETransferServer.Common;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.Token;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.ThirdPart.Exchange;

public class CoinGeckoProvider : IExchangeProvider
{
    private const string FiatCurrency = "usd";
    private const string SimplePriceUri = "/simple/price";

    private readonly ILogger<CoinGeckoProvider> _logger;
    private readonly IOptionsMonitor<CoinGeckoOptions> _coinGeckoOptions;
    private readonly IHttpProvider _httpProvider;

    public CoinGeckoProvider(IOptionsMonitor<CoinGeckoOptions> coinGeckoOptions, IHttpProvider httpProvider,
        ILogger<CoinGeckoProvider> logger)
    {
        _coinGeckoOptions = coinGeckoOptions;
        _httpProvider = httpProvider;
        _logger = logger;
    }


    public ExchangeProviderName Name()
    {
        return ExchangeProviderName.CoinGecko;
    }

    public async Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol)
    {
        var from = MappingSymbol(fromSymbol);
        var to = MappingSymbol(toSymbol);
        var url = _coinGeckoOptions.CurrentValue.BaseUrl + SimplePriceUri;
        _logger.LogDebug("CoinGecko url {Url}", url);
        
        var price = await _httpProvider.InvokeAsync<Price>(HttpMethod.Get,
            _coinGeckoOptions.CurrentValue.BaseUrl + SimplePriceUri,
            header: new Dictionary<string, string>
            {
                ["x-cg-pro-api-key"] = _coinGeckoOptions.CurrentValue.ApiKey
            },
            param: new Dictionary<string, string>
            {
                ["ids"] = string.Join(CommonConstant.Comma, from, to),
                ["vs_currencies"] = FiatCurrency
            });
        AssertHelper.IsTrue(price.ContainsKey(from), "CoinGecko not support symbol {}", from);
        AssertHelper.IsTrue(price.ContainsKey(to), "CoinGecko not support symbol {}", to);
        var exchange = price[from][FiatCurrency] / price[to][FiatCurrency];
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Exchange = (decimal)exchange,
            Timestamp = DateTime.UtcNow.ToUtcMilliSeconds()
        };
    }
    
    public async Task<List<TokenExchangeDto>> LatestAsync(List<string> fromSymbol, string toSymbol)
    {
        fromSymbol = fromSymbol.ConvertAll(item => MappingSymbol(item).ToLower()).Distinct().ToList();
        var from = fromSymbol.JoinAsString(CommonConstant.Comma);
        var to = MappingSymbol(toSymbol);
        var url = _coinGeckoOptions.CurrentValue.BaseUrl + SimplePriceUri;
        _logger.LogDebug("CoinGecko url {Url}", url);
        
        var price = await _httpProvider.InvokeAsync<Price>(HttpMethod.Get,
            _coinGeckoOptions.CurrentValue.BaseUrl + SimplePriceUri,
            header: new Dictionary<string, string>
            {
                ["x-cg-pro-api-key"] = _coinGeckoOptions.CurrentValue.ApiKey
            },
            param: new Dictionary<string, string>
            {
                ["ids"] = string.Join(CommonConstant.Comma, from, to),
                ["vs_currencies"] = FiatCurrency
            });
        AssertHelper.IsTrue(price.ContainsKey(to), "CoinGecko not support symbol {}", to);
        var exchangeList = new List<TokenExchangeDto>();
        foreach (var item in fromSymbol)
        {
            if(item.Equals(to)) continue;
            var exchange = price[item][FiatCurrency] / price[to][FiatCurrency];
            exchangeList.Add(new TokenExchangeDto
            {
                FromSymbol = item,
                ToSymbol = toSymbol,
                Exchange = (decimal)exchange,
                Timestamp = DateTime.UtcNow.ToUtcMilliSeconds()
            });
        }

        return exchangeList;
    }

    private string MappingSymbol(string sourceSymbol)
    {
        return _coinGeckoOptions?.CurrentValue?.CoinIdMapping?.TryGetValue(sourceSymbol, out var result) ?? false
            ? result
            : sourceSymbol;
    }

    public Task<TokenExchangeDto> HistoryAsync(string fromSymbol, string toSymbol, long timestamp)
    {
        throw new NotSupportedException();
    }
}