using System.Collections.Generic;

namespace ETransferServer.Options;

public class SupportedChainTokensOptions
{
    // chain id -> token
    public Dictionary<string, SupportTokenInfo> Tokens { get; set; } = new();
    // symbol -> status
    public Dictionary<string, StatusInfo> Transfer { get; set; } = new();
}

public class SupportTokenInfo
{
    // symbol -> status
    public Dictionary<string, StatusInfo> Deposit { get; set; } = new();
    public Dictionary<string, StatusInfo> Withdraw { get; set; } = new();
}

public class StatusInfo
{
    public bool IsOpen { get; set; } = true;
}