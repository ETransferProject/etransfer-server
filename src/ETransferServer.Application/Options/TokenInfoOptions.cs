using System.Collections.Generic;

namespace ETransferServer.Options;

public class TokenInfoOptions : Dictionary<string, SupportChainInfo>
{
}

public class SupportChainInfo
{
    public List<string> Deposit { get; set; } = new();
    public List<string> Withdraw { get; set; } = new();
}