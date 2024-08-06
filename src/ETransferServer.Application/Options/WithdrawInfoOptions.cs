using System.Collections.Generic;

namespace ETransferServer.Options;

public class WithdrawInfoOptions
{
    public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;
    public bool CanCrossSameChain { get; set; }
    public Dictionary<string, decimal> MinThirdPartFee { get; set; } = new();
    public string MinMemoVersion { get; set; } = "2.11.0";
    public decimal MinWithdraw { get; set; } = 0.2M;
    public decimal FeeFluctuationPercent { get; set; } = 0.1M;
    public int ThirdPartFeeExpireSeconds { get; set; } = 180;
}