using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Info;

public class GetOverviewRequestDto
{
    [Range(0, 2)]
    [Required]
    public int Type { get; set; }
    public int? MaxResultCount { get; set; }
}