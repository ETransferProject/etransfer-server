namespace ETransferServer.Grains.State.Users;

public class UserReconciliationState
{
    public Guid Id { get; set; }
    public string UserName { get; set; }
    public string Address { get; set; }
    public string PasswordHash { get; set; }
}

