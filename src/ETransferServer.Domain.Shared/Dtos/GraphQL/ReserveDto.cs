namespace ETransferServer.Dtos.GraphQL;

public class ReserveDto
{
    public string SymbolA { get; set; }
    public string SymbolB { get; set; }
    public long TimeStamp { get; set; }
    public long ReserveA { get; set; }
    public long ReserveB { get; set; }
}