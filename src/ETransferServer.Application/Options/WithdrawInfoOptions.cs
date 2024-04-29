namespace ETransferServer.Options;

public class WithdrawInfoOptions
{
    public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;
    public bool CanCrossSameChain { get; set; }
}