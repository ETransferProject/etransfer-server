namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class WithdrawTimerState
{
    [Id(0)] public Dictionary<Guid, WithdrawInfo> WithdrawInfoMap { get; set; }= new();
    [Id(1)] public Dictionary<Guid, WithdrawRequestInfo> WithdrawRequestMap { get; set; } = new();
}

[GenerateSerializer]
public class WithdrawInfo
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public long RequestTime { get; set; }
    [Id(2)] public long ExtraRequestTime { get; set; }
    [Id(3)] public long LaterRequestTime { get; set; }
    [Id(4)] public int RetryCount { get; set; }
}

[GenerateSerializer]
public class WithdrawRequestInfo
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public DateTime LastRequestTime { get; set; }
    [Id(2)] public int RetryCount { get; set; }
    [Id(3)] public bool Success { get; set; }
    [Id(4)] public Dictionary<string, string> ErrorDic { get; set; } = new();
}