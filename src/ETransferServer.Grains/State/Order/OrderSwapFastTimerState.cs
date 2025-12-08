namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderSwapFastTimerState
{
    [Id(0)] public Dictionary<Guid, TimerTransaction> OrderTransactionDict = new();
}