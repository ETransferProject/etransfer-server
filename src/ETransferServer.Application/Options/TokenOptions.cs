using System.Collections.Generic;

namespace ETransferServer.Options;

public class TokenOptions
{
    public Dictionary<string, List<TokenConfig>> Deposit { get; set; }
    public Dictionary<string, List<TokenConfig>> Withdraw { get; set; }
}

public class TokenConfig
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string Icon { get; set; }
    public string ContractAddress { get; set; }
}