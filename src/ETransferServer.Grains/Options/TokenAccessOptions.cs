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
}