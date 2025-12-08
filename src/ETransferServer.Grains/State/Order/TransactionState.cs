namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class TransactionState
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public DateTime CreateTime { get; set; }
}