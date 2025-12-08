using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Order;

public partial class OrderDepositAppService
{
    public async Task<FlowBehavior> HandleGetInfoExceptionAsync(Exception ex, GetDepositRequestDto request)
    {
        _logger.LogError(ex, "GetDepositInfo error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleBulkExceptionAsync(Exception ex, List<DepositOrderDto> dtoList)
    {
        _logger.LogError(ex, "Bulk save depositOrderIndex fail: {Count}", dtoList.Count);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, DepositOrderDto dto)
    {
        _logger.LogError(ex, "Save depositOrderIndex fail: {id}", dto.Id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}