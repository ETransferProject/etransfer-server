using Orleans;

namespace ETransferServer.Dtos.Token;

[GenerateSerializer]
public class CoBoCoinDto
{
    [Id(0)] public string Coin { get; set; }
    [Id(1)] public string DisplayName { get; set; }
    [Id(2)] public string Description { get; set; }
    [Id(3)] public string Balance { get; set; }
    [Id(4)] public int Decimal { get; set; }
    [Id(5)] public bool CanDeposit { get; set; }
    [Id(6)] public bool CanWithDraw { get; set; }
    [Id(7)] public bool requireMemo { get; set; }
    [Id(8)] public string AbsBalance { get; set; }
    [Id(9)] public string FeeCoin { get; set; }
    [Id(10)] public string AbsEstimateFee { get; set; }
    [Id(11)] public int ConfirmingThreshold { get; set; }
    [Id(12)] public int DustThreshold { get; set; }
    [Id(13)] public string TokenAddress { get; set; }
    
    [Id(14)] public long LastModifyTime { get; set; }
    [Id(15)] public long ExpireTime { get; set; }
}