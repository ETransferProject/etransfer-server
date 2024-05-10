namespace ETransferServer.Grains.Options;

public class SwapInfosOptions
{
    public Dictionary<string, SwapInfo> SwapInfos { get; set; }
}

public class SwapInfo
{
    public string Router { get; set; }
    public List<string> Path { get; set; }
    public decimal Slippage { get; set; }
    public decimal SubsidyProportion { get; set; }
    public long SubsidyMax { get; set; }
}