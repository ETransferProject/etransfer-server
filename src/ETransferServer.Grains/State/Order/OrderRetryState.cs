namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderRetryState
{
    [Id(0)] public Dictionary<Guid, OrderRetryFrom> OrderRetryData = new();
}

[GenerateSerializer]
public class DepositOrderRetryState : OrderRetryState
{
}

[GenerateSerializer]
public class WithdrawOrderRetryState : OrderRetryState
{
}

[GenerateSerializer]
public class OrderRetryFrom
{
    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public string RetryFromState { get; set; }
    
}