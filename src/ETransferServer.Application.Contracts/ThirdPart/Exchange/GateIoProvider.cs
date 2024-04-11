using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.Token;
using ETransferServer.Options;
using ETransferServer.Samples.HttpClient;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ThirdPart.Exchange;

public class GateIoProvider : IExchangeProvider, ISingletonDependency
{
    private readonly IOptionsSnapshot<ExchangeOptions> _exchangeOptions;
    private readonly IHttpProvider _httpProvider;

    public GateIoProvider(IOptionsSnapshot<ExchangeOptions> exchangeOptions, IHttpProvider httpProvider)
    {
        _exchangeOptions = exchangeOptions;
        _httpProvider = httpProvider;
    }

    public static class Api
    {
        public static readonly ApiInfo Candlesticks = new(HttpMethod.Get, "/api/v4/spot/candlesticks");
    }

    public ExchangeProviderName Name()
    {
        return ExchangeProviderName.GateIo;
    }

    public async Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol)
    {


        var from = SymbolMapping(fromSymbol);
        var to = SymbolMapping(toSymbol);
        if (from == to)
        {
            return TokenExchangeDto.One(fromSymbol, toSymbol, DateTime.UtcNow.ToUtcMilliSeconds());
        }
        var resp = await _httpProvider.InvokeAsync<List<List<string>>>(_exchangeOptions.Value.GateIo.BaseUrl,
            Api.Candlesticks, param: new Dictionary<string, string>
            {
                ["currency_pair"] = string.Join(CommonConstant.Underline, from, to),
                ["limit"] = "1",
                ["interval"] = Interval.Minute1
            });
        AssertHelper.NotEmpty(resp, "Empty result");
        var klineData = new CandlesticksResponse(resp[0]);
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Timestamp = klineData.TimestampSeconds * 1000,
            Exchange = klineData.WindowClosed ? (klineData.OpeningPrice + klineData.ClosingPrice) / 2 : klineData.ClosingPrice
        };
    }

    public async Task<List<TokenExchangeDto>> LatestAsync(List<string> fromSymbol, string toSymbol)
    {
        var to = SymbolMapping(toSymbol);
        var tasks = new List<Task<TokenExchangeDto>>();
        foreach (var from in fromSymbol)
        {
            var mappingFrom = SymbolMapping(from);
            tasks.Add(LatestAsync(mappingFrom, to));
        }
        var respList = await Task.WhenAll(tasks);
        return respList.ToList();
    }

    public async Task<TokenExchangeDto> HistoryAsync(string fromSymbol, string toSymbol, long timestamp)
    {
        var from = SymbolMapping(fromSymbol);
        var to = SymbolMapping(toSymbol);
        if (from == to)
        {
            return TokenExchangeDto.One(fromSymbol, toSymbol, timestamp);
        }
        var resp = await _httpProvider.InvokeAsync<List<List<string>>>(_exchangeOptions.Value.GateIo.BaseUrl,
            Api.Candlesticks, param: new Dictionary<string, string>
            {
                ["currency_pair"] = string.Join(CommonConstant.Underline, from, to),
                ["from"] = (timestamp / 1000).ToString(),
                ["interval"] = Interval.Hour1
            });
        AssertHelper.NotEmpty(resp, "Empty result");
        var klineData = new CandlesticksResponse(resp[0]);
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Timestamp = klineData.TimestampSeconds * 1000,
            Exchange = klineData.WindowClosed ? (klineData.OpeningPrice + klineData.ClosingPrice) / 2 : klineData.ClosingPrice
        };
    }

    private string SymbolMapping(string standardSymbol)
    {
        return _exchangeOptions.Value.GateIo.SymbolMapping.GetValueOrDefault(standardSymbol, standardSymbol);
    }

    public class CandlesticksResponse
    {
        public long TimestampSeconds { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal ClosingPrice { get; set; }
        public decimal HighestPrice { get; set; }
        public decimal LowestPrice { get; set; }
        public decimal OpeningPrice { get; set; }
        public decimal BaseCurrencyTradingVolume { get; set; }
        public bool WindowClosed { get; set; }

        public CandlesticksResponse()
        {
        }

        public CandlesticksResponse(List<string> vals)
        {
            TimestampSeconds = vals[0].SafeToLong();
            TransactionAmount = vals[1].SafeToDecimal();
            ClosingPrice = vals[2].SafeToDecimal();
            HighestPrice = vals[3].SafeToDecimal();
            LowestPrice = vals[4].SafeToDecimal();
            OpeningPrice = vals[5].SafeToDecimal();
            BaseCurrencyTradingVolume = vals[6].SafeToDecimal();
            WindowClosed = vals[7].SafeToBool();
        }
    }


    public static class Interval
    {
        public static string Second10 = "10s";
        public static string Minute1 = "1m";
        public static string Minute5 = "5m";
        public static string Minute15 = "15m";
        public static string Minute30 = "30m";
        public static string Hour1 = "1h";
        public static string Hour4 = "4h";
        public static string Hour8 = "8h";
        public static string Day1 = "1d";
        public static string Day7 = "7d";
        public static string Day30 = "30d";
    }
}