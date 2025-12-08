namespace ETransferServer.Grains.State.Swap;

[GenerateSerializer]
public class EvmTransactionState
{
    [Id(0)] public string TxId { get; set; }
    [Id(1)] public long BlockTime { get; set; }
}