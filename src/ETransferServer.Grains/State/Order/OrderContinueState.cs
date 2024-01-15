namespace ETransferServer.Grains.State.Order;

public class OrderRetryState
{
    
    public Dictionary<Guid, OrderRetryFrom> OrderRetryData = new();
    
    
}


public class OrderRetryFrom
{
    public Guid OrderId { get; set; }
    public string RetryFromState { get; set; }
    
}