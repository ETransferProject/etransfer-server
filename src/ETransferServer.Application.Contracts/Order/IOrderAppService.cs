using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Order;

public interface IOrderAppService
{
    Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request);
    Task<OrderStatusDto> GetOrderRecordStatusAsync();
    Task OrderRecordReadAsync();
}