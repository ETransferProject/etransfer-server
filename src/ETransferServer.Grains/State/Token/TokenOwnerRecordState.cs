using ETransferServer.Dtos.TokenAccess;

namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class TokenOwnerRecordState
{
    [Id(0)] public List<TokenOwnerDto> TokenOwnerList { get; set; } = new();
    [Id(1)] public long? UpdateTime { get; set; }
    [Id(2)] public long? CreateTime { get; set; }
}