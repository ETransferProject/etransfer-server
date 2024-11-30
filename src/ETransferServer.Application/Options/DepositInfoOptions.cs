using System.Collections.Generic;

namespace ETransferServer.Options;

public class DepositInfoOptions
{
    public int AssignedAddressExpiredHour { get; set; } = 48;
    public Dictionary<string, List<string>> TransferAddressLists { get; set; }
    public Dictionary<string, string> TxPairType { get; set; } = new()
    {
        ["0"] = "USDT_SGR-1"
    };
}