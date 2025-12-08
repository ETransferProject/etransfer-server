namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderSwapTimerState
{
    [Id(0)] public Dictionary<Guid, TimerTransaction> OrderTransactionDict = new();
}