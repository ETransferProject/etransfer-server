using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.User;
using Microsoft.Extensions.Logging;

namespace ETransferServer.User;

public partial class UserAddressService
{
    public async Task<FlowBehavior> HandleBulkExceptionAsync(Exception ex, List<UserAddressDto> dtoList)
    {
        _logger.LogError(ex, "Bulk save userAddressIndex fail: {count}", dtoList.Count);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, UserAddressDto dto)
    {
        _logger.LogError(ex, "Save userAddressIndex fail: {id}", dto.Id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}