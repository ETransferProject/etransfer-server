using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Info;

public class GetTransferRequestDto : PagedAndSortedResultRequestDto
{
    [Range(0, 2)]
    [Required]
    public int Type { get; set; }
    [Range(0, int.MaxValue)]
    [Required]
    public int FromToken { get; set; }
    [Range(0, int.MaxValue)]
    [Required]
    public int FromChainId { get; set; }
    [Range(0, int.MaxValue)]
    [Required]
    public int ToToken { get; set; }
    [Range(0, int.MaxValue)]
    [Required]
    public int ToChainId { get; set; }
    [Range(0, int.MaxValue)]
    public int Limit { get; set; } = 50;
}