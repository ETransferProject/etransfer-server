namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderTimerState
{
    [Id(0)] public Dictionary<Guid, TimerTransaction> OrderTransactionDict = new();
}

[GenerateSerializer]
public class DepositOrderTimerState : OrderTimerState
{
}

[GenerateSerializer]
public class WithdrawOrderTimerState : OrderTimerState
{
}

[GenerateSerializer]
public class TimerTransaction
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public string TxId { get; set; }
    [Id(2)] public long? TxTime { get; set; }
    [Id(3)] public string TransferType { get; set; }
    [Id(4)] public bool IsForward { get; set; } = true;
}

public enum TransferTypeEnum
{
    ToTransfer,
    FromTransfer
}