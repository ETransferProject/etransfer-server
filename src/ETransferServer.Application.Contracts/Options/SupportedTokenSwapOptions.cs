using System.Collections.Generic;

namespace ETransferServer.Options;

public class SupportedTokenSwapOptions
{
    // symbol -> to token list
    public Dictionary<string,List<TargetTokenConfig>> SwapTokenMap { get; set; }
}

public class TargetTokenConfig
{
    public string Symbol { get; set; }
    public List<string> ChainIdList { get; set; } = new();
}