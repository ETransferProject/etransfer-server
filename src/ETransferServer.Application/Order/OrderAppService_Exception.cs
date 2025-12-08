using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Order;

public partial class OrderAppService
{
    public async Task<FlowBehavior> HandleGetListExceptionAsync(Exception ex, GetOrderRecordRequestDto request)
    {
        _logger.LogError(ex, "Get order record list failed, type={Type}, status={Status}",
            request.Type, request.Status);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PagedResultDto<OrderIndexDto>()
        };
    }
    
    public async Task<FlowBehavior> HandleGetDetailExceptionAsync(Exception ex, string id)
    {
        _logger.LogError(ex, "Get order record detail failed, orderId={id}", id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new OrderDetailDto()
        };
    }
    
    public async Task<FlowBehavior> HandleGetUserListExceptionAsync(Exception ex, GetUserOrderRecordRequestDto request, 
        OrderChangeEto orderEto)
    {
        _logger.LogError(ex, "Get user order record list failed, address={Address}, time={Time}", request.Address, request.Time);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new UserOrderDto
            {
                Address = request.Address
            }
        };
    }
    
    public async Task<FlowBehavior> HandleGetStatusExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get order record status failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new OrderStatusDto()
        };
    }
}