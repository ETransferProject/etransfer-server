using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Order;

public interface IOrderAppService
{
    Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request);
    Task<OrderDetailDto> GetOrderRecordDetailAsync(string id);
    Task<UserOrderDto> GetUserOrderRecordListAsync(GetUserOrderRecordRequestDto request, OrderChangeEto orderEto = null);
    Task<OrderStatusDto> GetOrderRecordStatusAsync();
}