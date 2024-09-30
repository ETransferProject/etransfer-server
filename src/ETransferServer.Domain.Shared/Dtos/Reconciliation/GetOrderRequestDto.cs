using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Reconciliation;

public class GetOrderRequestDto : PagedAndSortedResultRequestDto
{
    public string? Address { get; set; }
    [Range(0, int.MaxValue)]
    public int Token { get; set; }
    [Range(0, int.MaxValue)]
    public int FromChainId { get; set; }
    [Range(0, int.MaxValue)]
    public int ToChainId { get; set; }
    public long? StartCreateTime { get; set; }
    public long? EndCreateTime { get; set; }
}