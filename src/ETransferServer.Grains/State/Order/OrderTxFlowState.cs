namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class OrderTxFlowState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Dictionary<string, OrderTxData> OrderTxInfo = new();
}

[GenerateSerializer]
public class OrderTxData
{
    [Id(0)] public string TxId { get; set; }
    [Id(1)] public string ChainId { get; set; }
    [Id(2)] public string Status { get; set; }
    
}