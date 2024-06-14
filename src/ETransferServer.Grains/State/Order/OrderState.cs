namespace ETransferServer.Grains.State.Order;

public class OrderState
{
    public long LastTime { get; set; }
    public List<string> ExistOrders { get; set; } = new();
}