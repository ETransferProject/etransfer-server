namespace ETransferServer.Models;

public class GetTokenListRequestDto 
{
    public string Type { get; set; }
    public string? ChainId { get; set; }
}
