namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class WithdrawCoboTimerState
{
    [Id(0)] public Dictionary<Guid, WithdrawRequestInfo> WithdrawRequestMap { get; set; } = new();
}