namespace ETransferServer.Grains.Options;

public class WithdrawOptions
{
    public bool IsOpen { get; set; } = true;
    public long WithdrawThreshold { get; set; } = 100000;
    public string OrderChangeTopic { get; set; }
    public string WithdrawFeeAlarmTemplate { get; set; }
    public decimal MinThirdPartFee { get; set; } = new (0.2);
    public decimal FeeFluctuationPercent { get; set; } = (decimal)0.1;
    public int ThirdPartFeeExpireSeconds { get; set; } = 180;
    public int ToTransferMaxRetry { get; set; } = 5;
    public int MaxListLength { get; set; } = 1000;
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
    public Dictionary<string, TransactionThreshold> Homogeneous { get; set; } = new();
}

public class TransactionThreshold
{
    public long AmountThreshold { get; set; } = 300;
    public long BlockHeightUpperThreshold { get; set; } = 300;
    public long BlockHeightLowerThreshold { get; set; } = 30;
}