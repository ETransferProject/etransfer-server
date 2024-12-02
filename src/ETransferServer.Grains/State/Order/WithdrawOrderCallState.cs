namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class WithdrawOrderCallState
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public int Status { get; set; }
    [Id(2)] public int CallRetry { get; set; }
    [Id(3)] public int CallbackRetry { get; set; }
    [Id(4)] public int CallQueryRetry { get; set; }
}