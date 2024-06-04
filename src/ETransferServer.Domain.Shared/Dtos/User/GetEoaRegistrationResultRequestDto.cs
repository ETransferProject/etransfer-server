using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.User;

public class GetEoaRegistrationResultRequestDto
{
    [Required]
    public string Address { get; set; }
}