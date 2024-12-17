using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.TokenAccess;

public class GetTokenApplyOrderListInput : PagedAndSortedResultRequestDto
{
    public string? Status { get; set; }
}