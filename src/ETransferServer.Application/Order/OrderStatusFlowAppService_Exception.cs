using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Order;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Order;

public partial class OrderStatusFlowAppService
{
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, OrderStatusFlowDto dto)
    {
        _logger.LogError(ex, "Save depositOrderIndex fail: {id}", dto.Id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}