using ETransferServer.User.Dtos;

namespace ETransferServer.Grains.Grain.Users;

[GenerateSerializer]
public class UserGrainDto
{
    [Id(0)] public string AppId { get; set; }
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public string CaHash { get; set; }
    [Id(3)] public List<AddressInfo> AddressInfos { get; set; }
    [Id(4)] public long CreateTime { get; set; }
    [Id(5)] public long ModificationTime { get; set; }
}