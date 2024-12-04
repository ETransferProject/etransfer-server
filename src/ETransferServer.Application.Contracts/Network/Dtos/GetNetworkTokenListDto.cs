using System.Collections.Generic;

namespace ETransferServer.Network.Dtos;

public class GetNetworkTokenListDto : Dictionary<string, NetworkTokenListDto>
{
}

public class NetworkTokenListDto : Dictionary<string, List<NetworkBasicDto>>
{
}

public class NetworkBasicDto
{
    public string Network { get; set; }
    public string Name { get; set; }
}