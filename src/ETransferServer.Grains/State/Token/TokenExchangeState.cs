using ETransferServer.Dtos.Token;

namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class TokenExchangeState
{
    [Id(0)] public long LastModifyTime { get; set; }
    [Id(1)] public long ExpireTime { get; set; }
    [Id(2)] public Dictionary<string, TokenExchangeDto> ExchangeInfos { get; set; }
}