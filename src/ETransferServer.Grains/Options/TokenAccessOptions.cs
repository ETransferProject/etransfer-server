namespace ETransferServer.Grains.Options;

public class TokenAccessOptions
{
    public string ScanBaseUrl { get; set; }
    public string ScanTokenListUri { get; set; }
    public string ScanTokenDetailUri { get; set; }
    public string SymbolMarketBaseUrl { get; set; }
    public string SymbolMarketUserTokenListUri { get; set; }
    public string SymbolMarketUserThirdTokenListUri { get; set; }
    public string SymbolMarketPrepareBindingUri { get; set; }
    public string SymbolMarketBindingUri { get; set; }
    public string AwakenBaseUrl { get; set; }
    public string AwakenGetTokenLiquidityUri { get; set; }
    public int DataExpireSeconds { get; set; } = 180;
    public string HashVerifyKey { get; set; }
    public AvailableTokenConfigDto DefaultConfig { get; set; } = new();
    public Dictionary<string, AvailableTokenConfigDto> TokenConfig { get; set; } = new();
}

public class AvailableTokenConfigDto
{
    public string Liquidity { get; set; } = "1000";
    public int Holders { get; set; } = 1000;
}