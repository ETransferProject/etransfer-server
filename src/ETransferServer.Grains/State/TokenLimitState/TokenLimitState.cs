namespace ETransferServer.Grains.State.TokenLimitState;

[GenerateSerializer]
public class TokenLimitState
{
    [Id(0)] public decimal RemainingLimit { get; set; }
    [Id(1)] public bool HasInit { get; set; }
}