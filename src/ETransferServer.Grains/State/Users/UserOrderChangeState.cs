

namespace ETransferServer.Grains.State.Users;

[GenerateSerializer]
public class UserOrderChangeState
{
    [Id(0)] public Guid? Id { get; set; }
    [Id(1)] public string Address { get; set; }
    [Id(2)] public long? Time { get; set; }
}

