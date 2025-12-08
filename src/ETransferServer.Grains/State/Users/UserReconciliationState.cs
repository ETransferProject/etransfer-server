namespace ETransferServer.Grains.State.Users;

[GenerateSerializer]
public class UserReconciliationState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string UserName { get; set; }
    [Id(2)] public string Address { get; set; }
    [Id(3)] public string PasswordHash { get; set; }
}

