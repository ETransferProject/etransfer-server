namespace ETransferServer.Dtos.Token;

public class TokenDto
{
    public string Symbol { get; set; }
    public string TokenName { get; set; }
    public long TotalSupply { get; set; }
    public int Decimals { get; set; }
    public bool IsBurnable { get; set; }
    public long IssueChainId { get; set; }

}