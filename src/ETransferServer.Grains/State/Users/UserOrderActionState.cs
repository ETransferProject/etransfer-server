

namespace ETransferServer.Grains.State.Users;

public class UserOrderActionState
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public long LastModifyTime { get; set; }
}

