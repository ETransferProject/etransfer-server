using System.Collections.Generic;
using ETransferServer.Common;

namespace ETransferServer.Options;

public class TokenSupportedChainInfoOptions
{
    // symbol -> supported chains
    public Dictionary<string, List<SupportedChainInfo>> SupportedChains { get; set; } = new();
}

public class SupportedChainInfo
{
    public string Network { get; set; } = string.Empty;

    public List<string> SupportedType { get; set; } =
    [
        OrderTypeEnum.Deposit.ToString(),
        OrderTypeEnum.Withdraw.ToString(), 
        OrderTypeEnum.Transfer.ToString()
    ];

    public List<string> SupportWhiteList { get; set; }
    public List<string> SupportChain { get; set; }
}