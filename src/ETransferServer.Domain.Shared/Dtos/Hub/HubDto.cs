using System.Collections.Generic;

namespace ETransferServer.Dtos.Hub;

public class HubDto
{
    public List<string> ConnectionIds { get; set; } = new();
    public string ClientId { get; set; }
    public long ExpireTime { get; set; }
}