namespace ETransferServer.Grains.State.Order;

public class OrderTimerState
{

    public Dictionary<Guid, TimerTransaction> OrderTransactionDict = new();

}


public class TimerTransaction
{
    public string ChainId { get; set; }
    public string TxId { get; set; }
    public long? TxTime { get; set; }
    public string TransferType { get; set; }
    public bool IsForward { get; set; } = true;
}

public enum TransferTypeEnum
{
    ToTransfer,
    FromTransfer
}