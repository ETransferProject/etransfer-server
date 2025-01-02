using System.Collections.Generic;

namespace ETransferServer.Options;

public class DepositInfoOptions
{
    public int AssignedAddressExpiredHour { get; set; } = 48;
    public ServiceFeeDto ServiceFee { get; set; } = new();
    public Dictionary<string, List<string>> TransferAddressLists { get; set; }
    public Dictionary<string, string> TxPairType { get; set; } = new()
    {
        ["0"] = "USDT_SGR-1"
    };
}

public class ServiceFeeDto
{
    public bool IsOpen { get; set; }
    public Dictionary<string, decimal> AmountThreshold { get; set; } = new();
}