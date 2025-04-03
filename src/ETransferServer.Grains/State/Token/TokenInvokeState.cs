namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class TokenInvokeState
{
    [Id(0)] public long LastModifyTime { get; set; }
    [Id(1)] public Guid UserTokenIssueId { get; set; } = Guid.Empty;
}