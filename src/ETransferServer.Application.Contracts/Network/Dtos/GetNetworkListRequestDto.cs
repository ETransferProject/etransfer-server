namespace ETransferServer.Models;

public class GetNetworkListRequestDto 
{
    public string Type { get; set; }
    public string? ChainId { get; set; }
    public string? Symbol { get; set; }
    public string? Address { get; set; }
}