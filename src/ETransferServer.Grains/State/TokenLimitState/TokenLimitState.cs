namespace ETransferServer.Grains.State.TokenLimitState;

public class TokenLimitState
{
    public decimal RemainingLimit { get; set; }
    public bool HasInit { get; set; }
}