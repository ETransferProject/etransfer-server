

namespace ETransferServer.Grains.State.Users;

[GenerateSerializer]
public class UserOrderActionState
{
    [Id(0)] public Guid? Id { get; set; }
    [Id(1)] public string UserId { get; set; }
    [Id(2)] public long LastModifyTime { get; set; }
}

