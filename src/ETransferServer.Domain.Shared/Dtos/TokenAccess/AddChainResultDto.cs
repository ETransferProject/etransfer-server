using System.Collections.Generic;

namespace ETransferServer.Dtos.TokenAccess;

public class AddChainResultDto
{
    public List<AddChainDto> ChainList { get; set; }
    public List<AddChainDto> OtherChainList { get; set; }
}

public class AddChainDto
{
    public string Id { get; set; }
    public string ChainId { get; set; }
}