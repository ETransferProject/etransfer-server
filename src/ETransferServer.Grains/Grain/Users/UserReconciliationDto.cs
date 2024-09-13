namespace ETransferServer.Grains.Grain.Users;

public class UserReconciliationDto
{
    public string UserName { get; set; }
    public string Address { get; set; }
    public string PasswordHash { get; set; }
}