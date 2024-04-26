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
    public Dictionary<string, string> PaymentAddresses { get; set; } = new();
}