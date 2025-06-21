using System.Collections.Generic;

namespace ETransferServer.Options;

public class WithdrawInfoOptions
{
    public bool CanCrossSameChain { get; set; }
    public long WithdrawThreshold { get; set; } = 100000;
    public string OrderChangeTopic { get; set; }
    // for testnet
    public Dictionary<string, List<string>> SupportWhiteLists { get; set; }
    public int ToTransferMaxRetry { get; set; } = 5;
    public int CallMaxRetry { get; set; } = 5;
    public int CallbackMaxRetry { get; set; } = 5;
    public int CallQueryMaxRetry { get; set; } = 5;
    public int MaxListLength { get; set; } = 1000;
    public Dictionary<string, decimal> LargeAmount { get; set; } = new();
    public Dictionary<string, TransactionThreshold> Homogeneous { get; set; } = new();
}
public class TransactionThreshold
{
    public long AmountThreshold { get; set; } = 300;
    public long BlockHeightUpperThreshold { get; set; } = 300;
    public long BlockHeightLowerThreshold { get; set; } = 30;
    public decimal WithdrawFee { get; set; } = 0M;
}