namespace ETransferServer.Grains.Options;

public class SwapInfosOptions
{
    public Dictionary<string, SwapInfo> PairInfos { get; set; }
    public int SafeLibDiff { get; set; } = 300;
    public int CallTxRetryTimes { get; set; } = 3;
}

public class SwapInfo
{
    public string Router { get; set; }
    public string MethodName { get; set; } = "SwapExactTokensForTokens";
    public List<string> Path { get; set; }
    public decimal Slippage { get; set; }
    public decimal FeeRate { get; set; }
    public decimal SubsidyProportion { get; set; } = 0;
    public long SubsidyMax { get; set; } = 0;
}