using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Orders;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Order;

public interface IOrderAppService
{
    Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request);
    Task<OrderDetailDto> GetOrderRecordDetailAsync(string id);
    Task<Tuple<OrderDetailDto, OrderIndex>> GetOrderDetailAsync(string id, Guid? userId, bool includeAll = false);
    Task<UserOrderDto> GetUserOrderRecordListAsync(GetUserOrderRecordRequestDto request, OrderChangeEto orderEto = null);
    Task<OrderStatusDto> GetOrderRecordStatusAsync();
}