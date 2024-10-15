namespace ETransferServer.Grains.State.Order;

public class WithdrawTimerState
{
    public Dictionary<Guid, WithdrawInfo> WithdrawInfoMap { get; set; }= new();
    public Dictionary<Guid, WithdrawRequestInfo> WithdrawRequestMap { get; set; } = new();
}

public class WithdrawInfo
{
    public Guid OrderId { get; set; }
    public long RequestTime { get; set; }
    public long ExtraRequestTime { get; set; }
    public long LaterRequestTime { get; set; }
    public int RetryCount { get; set; }
}

public class WithdrawRequestInfo
{
    public Guid OrderId { get; set; }
    public DateTime LastRequestTime { get; set; }
    public int RetryCount { get; set; }
    public bool Success { get; set; }
    public Dictionary<string, string> ErrorDic { get; set; } = new();
}