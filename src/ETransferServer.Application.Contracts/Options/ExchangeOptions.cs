using System.Collections.Generic;
using ETransferServer.Common;
using ETransferServer.ThirdPart.Exchange;

namespace ETransferServer.Options;

public class ExchangeOptions
{
    public BinanceOptions Binance { get; set; }

    public OkxOptions Okx { get; set; }

    public GateIoOptions GateIo { get; set; }

    public UniswapV3Options UniswapV3 { get; set; }

    public int DataExpireSeconds { get; set; } = 60;

    public List<string> DefaultProviders { get; set; } = new()
    {
        ExchangeProviderName.CoinGecko.ToString(), ExchangeProviderName.Okx.ToString(),
        ExchangeProviderName.Binance.ToString()
    };

    public Dictionary<string, List<string>> SymbolProviders { get; set; } = new();

    public Dictionary<string, string> BottomExchange { get; set; } = new();

    public List<string> SymbolExchangeViaUSDT { get; set; } = new();

    public List<string> LimitLogs { get; set; } = new()
    {
        CommonConstant.DefaultConst.LimitLogs
    };
    
    public List<string> GetSymbolProviders(string fromSymbol, string toSymbol)
    {
        return SymbolProviders.TryGetValue(fromSymbol.ToUpper(), out var fromProvider) ? fromProvider :
            SymbolProviders.TryGetValue(toSymbol.ToUpper(), out var toProvider) ? toProvider : DefaultProviders;
    }

}

public class BinanceOptions
{
    public string BaseUrl { get; set; }
    public int Block429Seconds { get; set; } = 300;
}

public class OkxOptions
{
    public string BaseUrl { get; set; }
}

public class GateIoOptions
{
    public string BaseUrl { get; set; } = "https://api.gateio.ws";

    // standard symbol => GateIo symbol
    public Dictionary<string, string> SymbolMapping { get; set; } = new();
}

public class UniswapV3Options
{
    public string BaseUrl { get; set; } = "https://api.thegraph.com/subgraphs/name/uniswap/uniswap-v3";

    // standard symbol pare => pool id
    public Dictionary<string, string> PoolId { get; set; } = new();
    
    // standard symbol => Uniswap symbol
    public Dictionary<string, string> SymbolMapping { get; set; } = new();
}