namespace ETransferServer.Grains.State.Swap;

public class SwapReserveState
{
    public string PairAddress { get; set; }
    public long ReserveIn { get; set; }
    public long ReserveOut { get; set; }
    public long Timestamp { get; set; }
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
}