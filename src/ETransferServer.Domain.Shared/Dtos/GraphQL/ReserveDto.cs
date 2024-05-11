namespace ETransferServer.Dtos.GraphQL;

public class ReserveDto
{
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
    public long TimeStamp { get; set; }
    public long ReserveIn { get; set; }
    public long ReserveOut { get; set; }
}