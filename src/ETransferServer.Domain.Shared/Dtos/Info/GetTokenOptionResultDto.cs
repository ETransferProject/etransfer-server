using System.Collections.Generic;

namespace ETransferServer.Dtos.Info;

public class GetTokenOptionResultDto
{
    public List<NetworkOptionDto> NetworkList { get; set; } = new();
    public List<TokenConfigOptionDto> TokenList { get; set; } = new();
}

public class NetworkOptionDto
{
    public int Key { get; set; }
    public string Name { get; set; }
    public string Network { get; set; }
}

public class TokenConfigOptionDto
{
    public int Key { get; set; }
    public string Name { get; set; }
    public string Symbol { get; set; }
    public string Icon { get; set; }
}
