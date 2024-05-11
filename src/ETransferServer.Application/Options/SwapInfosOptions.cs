using System.Collections.Generic;

namespace ETransferServer.Options;

public class SwapInfosOptions
{
    public Dictionary<string, SwapInfo> PairInfos { get; set; }
}

public class SwapInfo
{
    public string Router { get; set; }
    public List<string> Path { get; set; }
    public decimal Slippage { get; set; }
}