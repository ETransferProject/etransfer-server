namespace ETransferServer.Grains.Options;

public class DepositAddressOptions
{
    public int RemainingThreshold { get; set; } = 50;
    public int MaxRequestNewAddressCount { get; set; } = 2;
    public int MaxAssignedTransferThreshold { get; set; } = 20;
    public List<string> AddressWhiteLists { get; set; } = new();
    public List<string> SupportCoins { get; set; } = new();
    public List<string> EVMCoins { get; set; } = new();
}