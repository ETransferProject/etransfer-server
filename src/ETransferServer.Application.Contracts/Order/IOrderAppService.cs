using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Order;

public interface IOrderAppService: IApplicationService
{
    Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request);
    Task<OrderStatusDto> GetOrderRecordStatusAsync();
}