namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class TokenConfigurationState
{
    [Id(0)]
    public string ChainId { get; set; }
    
    [Id(1)]
    public string Symbol { get; set; }
    
    [Id(2)]
    public bool DepositEnabled { get; set; } = true;
    
    [Id(3)]
    public bool WithdrawEnabled { get; set; } = true;
    
    [Id(4)]
    public bool TransferEnabled { get; set; } = true;
    
    [Id(5)]
    public DateTime LastUpdatedTime { get; set; }
}
