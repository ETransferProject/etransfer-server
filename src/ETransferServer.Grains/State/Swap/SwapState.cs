namespace ETransferServer.Grains.State.Swap;

public class SwapState
{ 
    public string ToChainId { get; set; }
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
    public decimal AmountIn { get; set; }
    // The order create time.
    public long? TimeStamp { get; set; }
    public long ReserveIn { get; set; }
    public long ReserveOut { get; set; }
    // The amount that can be swapped when an order is created.
    public decimal AmountOutPre { get; set; }
    // The amount that can be swapped now.
    public decimal AmountOutNow { get; set; }

    public long AmountOutMin { get; set; }

    // If project has subsidy.
    public decimal Subsidy { get; set; }
    public decimal SubsidyMax { get; set; }
    // After swap transaction completed，amount out.
    public decimal ActualSwappedAmountOut { get; set; }
}