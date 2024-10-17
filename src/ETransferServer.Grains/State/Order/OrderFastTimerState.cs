namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderFastTimerState
{
    [Id(0)] public Dictionary<Guid, TimerTransaction> OrderTransactionDict = new();
}