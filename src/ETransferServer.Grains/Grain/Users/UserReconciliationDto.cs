namespace ETransferServer.Grains.Grain.Users;

[GenerateSerializer]
public class UserReconciliationDto
{
    [Id(0)] public string UserName { get; set; }
    [Id(1)] public string Address { get; set; }
    [Id(2)] public string PasswordHash { get; set; }
}