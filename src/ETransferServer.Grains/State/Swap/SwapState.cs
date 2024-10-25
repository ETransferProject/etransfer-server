namespace ETransferServer.Grains.State.Swap;

[GenerateSerializer]
public class SwapState
{ 
    [Id(0)] public string ToChainId { get; set; }
    [Id(1)] public string SymbolIn { get; set; }
    [Id(2)] public string SymbolOut { get; set; }
    [Id(3)] public decimal AmountIn { get; set; }
    // The order create time.
    [Id(4)] public long? TimeStamp { get; set; }
    [Id(5)] public long ReserveIn { get; set; }
    [Id(6)] public long ReserveOut { get; set; }
    // The amount that can be swapped when an order is created.
    [Id(7)] public decimal AmountOutPre { get; set; }
    // The amount that can be swapped now.
    [Id(8)] public decimal AmountOutNow { get; set; }

    [Id(9)] public long AmountOutMin { get; set; }

    // If project has subsidy.
    [Id(10)] public decimal Subsidy { get; set; }
    [Id(11)] public decimal SubsidyMax { get; set; }
    // After swap transaction completed，amount out.
    [Id(12)] public decimal ActualSwappedAmountOut { get; set; }
}