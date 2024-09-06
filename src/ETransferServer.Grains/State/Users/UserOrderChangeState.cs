

namespace ETransferServer.Grains.State.Users;

public class UserOrderChangeState
{
    public Guid? Id { get; set; }
    public string Address { get; set; }
    public long? Time { get; set; }
}

