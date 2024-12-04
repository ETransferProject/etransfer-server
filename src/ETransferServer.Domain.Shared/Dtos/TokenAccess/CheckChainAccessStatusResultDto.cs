using System.Collections.Generic;

namespace ETransferServer.Dtos.TokenAccess;

public class CheckChainAccessStatusResultDto
{
    public List<ChainAccessInfo> OtherChainList { get; set; } = new();
    public List<ChainAccessInfo> ChainList { get; set; } = new();
}

public class ChainAccessInfo
{
    public string ChainId { get; set; }
    public string Status { get; set; }
}