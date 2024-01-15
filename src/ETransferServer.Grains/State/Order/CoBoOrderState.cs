namespace ETransferServer.Grains.State.Order;

public class CoBoOrderState
{
    public long LastTime { get; set; }
    public List<string> ExistOrders { get; set; } = new();
}