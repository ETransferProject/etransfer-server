using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Reconciliation;

public class GetUserDto
{
    [Required]
    public string Name { get; set; }
    public string Address { get; set; }
    [Required]
    public string Password { get; set; }
}