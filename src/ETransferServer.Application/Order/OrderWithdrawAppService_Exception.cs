using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace ETransferServer.Order;

public partial class OrderWithdrawAppService
{
    public async Task<FlowBehavior> HandleGetInfoExceptionAsync(Exception ex, GetWithdrawListRequestDto request, 
        string version = null)
    {
        _logger.LogError(ex,
            "Create withdraw order error, chainId:{chainId}, network:{network}, address:{address}, amount:{amount}, symbol:{symbol}",
            request.ChainId, request.Network, request.Address, request.Amount, request.Symbol);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleCreateWithdrawExceptionAsync(Exception ex, GetWithdrawOrderRequestDto request, 
        string version = null)
    {
        if (ex is UserFriendlyException)
        {
            _logger.LogWarning(ex, "Create withdraw order failed");
        }
        else
        {
            _logger.LogError(ex, "Create withdraw order failed");
        }
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleSaveExceptionAsync(Exception ex, WithdrawOrderDto dto)
    {
        _logger.LogError(ex, "Save withdrawOrderIndex fail: {id}", dto.Id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
}