using System.Collections.Generic;

namespace ETransferServer.Models;

public class GetNetworkTokenListRequestDto 
{
    public List<string>? NetworkList { get; set; }
    public List<string>? TokenList { get; set; }
    public string? Address { get; set; }
}