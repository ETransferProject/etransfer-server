namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class TokenInvokeState
{
    [Id(0)] public long LastModifyTime { get; set; }
    [Id(1)] public long ExpireTime { get; set; }
    [Id(2)] public string LiquidityInUsd { get; set; } = "0";
}