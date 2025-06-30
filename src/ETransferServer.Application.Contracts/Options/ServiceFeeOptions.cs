using System.Collections.Generic;

namespace ETransferServer.Options;

public class ServiceFeeOptions
{
    public bool IsOpen { get; set; } = true;
    public Dictionary<string, decimal> AmountThreshold { get; set; } = new();
    public Dictionary<string, decimal> MinThirdPartFee { get; set; } = new();
    public Dictionary<string, decimal> MaxThirdPartFee { get; set; } = new();
    public decimal FeeFluctuationPercent { get; set; } = (decimal)0.1;
    public int ThirdPartFeeExpireSeconds { get; set; } = 180;
    public Dictionary<string, decimal> MinAmount { get; set; } = new();
    public decimal MinWithdraw { get; set; } = 0.2M;
    public Dictionary<string, decimal> MinDeposit { get; set; } = new();
    public List<string> WithdrawFeeNetwork { get; set; }
    public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;

}