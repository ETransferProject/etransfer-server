using System.Collections.Generic;

namespace ETransferServer.Options;

public class TokenAccessOptions
{
    public AvailableTokenConfigDto DefaultConfig { get; set; } = new();
    public Dictionary<string, AvailableTokenConfigDto> TokenConfig { get; set; } = new();
}

public class AvailableTokenConfigDto
{
    public string Liquidity { get; set; } = "1000";
    public int Holders { get; set; } = 1000;
}