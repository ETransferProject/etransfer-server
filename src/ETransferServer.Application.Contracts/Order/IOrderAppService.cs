using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Orders;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Order;

public interface IOrderAppService
{
    Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request);
    Task<OrderDetailDto> GetOrderRecordDetailAsync(string id);
    Task<Tuple<OrderDetailDto, OrderIndex>> GetOrderDetailAsync(string id, bool includeAll = false);
    Task<OrderIndexDto> GetTransferOrderAsync(CoBoTransactionDto coBoTransaction);
    Task<bool> CheckTransferOrderAsync(CoBoTransactionDto coBoTransaction, long time);
    Task<UserOrderDto> GetUserOrderRecordListAsync(GetUserOrderRecordRequestDto request, OrderChangeEto orderEto = null);
    Task<OrderStatusDto> GetOrderRecordStatusAsync(GetOrderRecordStatusRequestDto request);
}