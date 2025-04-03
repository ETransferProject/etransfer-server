using ETransferServer.Common;

namespace ETransferServer.Grains.Options;

public class WithdrawOptions
{
    public bool IsOpen { get; set; } = true;
    public long WithdrawThreshold { get; set; } = 100000;
    public string OrderChangeTopic { get; set; }
    public Dictionary<string, List<string>> SupportWhiteLists { get; set; }
    public Dictionary<string, decimal> MinThirdPartFee { get; set; } = new();
    public Dictionary<string, decimal> LargeAmount { get; set; } = new();
    public decimal MinWithdraw { get; set; } = new (0.2);
    public decimal FeeFluctuationPercent { get; set; } = (decimal)0.1;
    public int ThirdPartFeeExpireSeconds { get; set; } = 180;
    public int ToTransferMaxRetry { get; set; } = 5;
    public int CallMaxRetry { get; set; } = 5;
    public int CallbackMaxRetry { get; set; } = 5;
    public int CallQueryMaxRetry { get; set; } = 5;
    public int MaxListLength { get; set; } = 1000;
    public Dictionary<string, int> TokenInfo { get; set; } = new()
    {
        [CommonConstant.Symbol.USDT] = 6, 
        [CommonConstant.Symbol.SGR] = 8, 
        [CommonConstant.Symbol.Elf] = 8
    };
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
    public Dictionary<string, TransactionThreshold> Homogeneous { get; set; } = new();
}

public class TransactionThreshold
{
    public long AmountThreshold { get; set; } = 300;
    public long BlockHeightUpperThreshold { get; set; } = 300;
    public long BlockHeightLowerThreshold { get; set; } = 30;
    public decimal WithdrawFee { get; set; } = 0M;
}