namespace ETransferServer.Grains.State.Swap;

[GenerateSerializer]
public class SwapReserveState
{
    [Id(0)] public string PairAddress { get; set; }
    [Id(1)] public long ReserveIn { get; set; }
    [Id(2)] public long ReserveOut { get; set; }
    [Id(3)] public long Timestamp { get; set; }
    [Id(4)] public string SymbolIn { get; set; }
    [Id(5)] public string SymbolOut { get; set; }
}