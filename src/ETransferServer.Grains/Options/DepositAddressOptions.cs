namespace ETransferServer.Grains.Options;

public class DepositAddressOptions
{
    public int RemainingThreshold { get; set; } = 50;
    public int MaxRequestNewAddressCount { get; set; } = 2;
    public int MaxAssignedTransferThreshold { get; set; } = 200;
    public int MaxRequestNewAddressRetry { get; set; } = 10;
    public int MaxRequestRetryTimes { get; set; } = 3;
    public int AssignedAddressExpiredHour { get; set; } = 48;
    public Dictionary<string, List<string>> TransferAddressLists { get; set; }
    public List<string> AddressWhiteLists { get; set; } = new();
    public List<string> SupportCoins { get; set; } = new();
    public List<string> EVMCoins { get; set; } = new();
}