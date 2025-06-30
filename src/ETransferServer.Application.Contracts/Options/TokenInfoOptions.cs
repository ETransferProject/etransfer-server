using System.Collections.Generic;

namespace ETransferServer.Options;

public class TokenInfoOptions
{
    // chain id -> symbol -> TokenInfoDto
    public Dictionary<string,Dictionary<string,TokenInfoDto>> Tokens { get; set; } = new();
}

public class TokenInfoDto
{
    public string Symbol { get; set; }
    public string Name { get; set; }
    public int Decimal { get; set; }
    public string TokenAddress { get; set; }
    public string TokenExploreUrl { get; set; }
    public string Icon { get; set; }
}