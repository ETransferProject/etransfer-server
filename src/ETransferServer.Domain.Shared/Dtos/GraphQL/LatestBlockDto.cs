namespace ETransferServer.Dtos.GraphQL;

public class LatestBlockDto : IndexerCommonResult<LatestBlockDto>
{
    public string ChainId { get; set; }
    public string BlockHash { get; set; }
    public long BlockHeight { get; set; }
    public string PreviousBlockHash { get; set; }
    public long BlockTime { get; set; }
    public bool Confirmed { get; set; }

}