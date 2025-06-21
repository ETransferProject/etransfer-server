using System.Collections.Generic;

namespace ETransferServer.Options;

public class SupportedTokenSwapOptions
{
    // symbol -> to token list
    public Dictionary<string,List<ToTokenConfig>> SwapTokenMap { get; set; }
}

public class ToTokenConfig
{
    public string Symbol { get; set; }
    public List<string> ChainIdList { get; set; } = new();
}