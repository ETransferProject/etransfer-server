namespace ETransferServer.Dtos.Token;

public class CoBoCoinDto
{
    public string Coin { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Balance { get; set; }
    public int Decimal { get; set; }
    public bool CanDeposit { get; set; }
    public bool CanWithDraw { get; set; }
    public bool requireMemo { get; set; }
    public string AbsBalance { get; set; }
    public string FeeCoin { get; set; }
    public string AbsEstimateFee { get; set; }
    public int ConfirmingThreshold { get; set; }
    public int DustThreshold { get; set; }
    public string TokenAddress { get; set; }
    
    public long LastModifyTime { get; set; }
    public long ExpireTime { get; set; }
}