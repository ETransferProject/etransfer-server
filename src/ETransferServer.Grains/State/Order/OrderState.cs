namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderState
{
    [Id(0)] public long LastTime { get; set; }
    [Id(1)] public List<string> ExistOrders { get; set; } = new();
}