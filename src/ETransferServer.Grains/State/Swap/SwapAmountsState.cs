namespace ETransferServer.Grains.State.Swap;

[GenerateSerializer]
public class SwapAmountsState
{
    [Id(0)] public decimal ConversionRate { get; set; }
}