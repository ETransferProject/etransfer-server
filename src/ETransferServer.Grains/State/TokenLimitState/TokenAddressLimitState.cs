namespace ETransferServer.Grains.State.TokenLimitState;

[GenerateSerializer]
public class TokenAddressLimitState
{
    [Id(0)] public int CurrentAssignedCount { get; set; }
}