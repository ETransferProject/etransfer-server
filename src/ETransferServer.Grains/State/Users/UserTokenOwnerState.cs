using ETransferServer.Dtos.TokenAccess;

namespace ETransferServer.Grains.State.Users;

[GenerateSerializer]
public class UserTokenOwnerState
{
    [Id(0)] public List<TokenOwnerDto> TokenOwnerList { get; set; } = new();
    [Id(1)] public string Address { get; set; }
    [Id(2)] public long? UpdateTime { get; set; }
    [Id(3)] public long? CreateTime { get; set; }
}