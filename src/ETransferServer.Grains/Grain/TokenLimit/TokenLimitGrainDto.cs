namespace ETransferServer.Grains.Grain.TokenLimit;

[GenerateSerializer]
public class TokenLimitGrainDto
{
    [Id(0)] public decimal RemainingLimit { get; set; }
}