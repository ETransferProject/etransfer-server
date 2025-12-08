using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.User;

public class GetEoaRegistrationResultRequestDto
{
    [Required]
    public string Address { get; set; }
}

public class GetRegistrationResultRequestDto : GetEoaRegistrationResultRequestDto
{
    [Required]
    public string SourceType { get; set; }
}