using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class AddChainResultDto
{
    [Id(0)] public List<AddChainDto> ChainList { get; set; }
    [Id(1)] public List<AddChainDto> OtherChainList { get; set; }
}

[GenerateSerializer]
public class AddChainDto
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string ChainId { get; set; }
}