using Orleans;

namespace ETransferServer.Dtos.GraphQL;

[GenerateSerializer]
public class ReserveDto
{
    [Id(0)] public string SymbolA { get; set; }
    [Id(1)] public string SymbolB { get; set; }
    [Id(2)] public long TimeStamp { get; set; }
    [Id(3)] public long ReserveA { get; set; }
    [Id(4)] public long ReserveB { get; set; }
}