using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Info;

public class GetTokenRequestDto
{
    [Range(0, 2)]
    [Required]
    public int Type { get; set; }
}