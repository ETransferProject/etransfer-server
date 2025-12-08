namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class TokenPoolTimerState
{
    [Id(0)] public long LastQueryTime { get; set; }
}