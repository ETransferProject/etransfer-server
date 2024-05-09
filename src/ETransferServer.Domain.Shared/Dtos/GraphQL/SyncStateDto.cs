namespace ETransferServer.Dtos.GraphQL;

public class SyncStateDto
{
    public long ConfirmedBlockHeight { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}