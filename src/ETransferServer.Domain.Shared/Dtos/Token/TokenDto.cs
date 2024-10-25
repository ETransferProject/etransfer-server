using Orleans;

namespace ETransferServer.Dtos.Token;

[GenerateSerializer]
public class TokenDto
{
    [Id(0)] public string Symbol { get; set; }
    [Id(1)] public string TokenName { get; set; }
    [Id(2)] public long TotalSupply { get; set; }
    [Id(3)] public int Decimals { get; set; }
    [Id(4)] public bool IsBurnable { get; set; }
    [Id(5)] public long IssueChainId { get; set; }

}