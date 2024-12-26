namespace ETransferServer.Dtos.TokenAccess;

public class TokenApplyOrderResultDto : TokenApplyOrderDto
{
    public long RejectedTime { get; set; }
    public string RejectedReason { get; set; }
    public long FailedTime { get; set; }
    public string FailedReason { get; set; }
}