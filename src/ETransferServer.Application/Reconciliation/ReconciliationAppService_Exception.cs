using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Reconciliation;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Reconciliation;

public partial class ReconciliationAppService
{
    public async Task<FlowBehavior> HandleChangePwdExceptionAsync(Exception ex, ChangePasswordRequestDto request)
    {
        _logger.LogError(ex, "Change password failed.");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleInitUserExceptionAsync(Exception ex, GetUserDto request)
    {
        _logger.LogError(ex, "Init user failed.");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetDetailExceptionAsync(Exception ex, string id)
    {
        _logger.LogError(ex, "Get rec order record detail failed, orderId={id}", id);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new OrderMoreDetailDto()
        };
    }
    
    public async Task<FlowBehavior> HandleGetListExceptionAsync(Exception ex, GetOrderRequestDto request)
    {
        _logger.LogError(ex, "Get rec deposit order record list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new OrderPagedResultDto<OrderRecordDto>()
        };
    }
    
    public async Task<FlowBehavior> HandleGetWithdrawListExceptionAsync(Exception ex, GetOrderRequestDto request)
    {
        _logger.LogError(ex, "Get rec withdraw order record list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new OrderPagedResultDto<OrderMoreDetailDto>()
        };
    }
    
    public async Task<FlowBehavior> HandleGetFailListExceptionAsync(Exception ex, GetOrderRequestDto request)
    {
        _logger.LogError(ex, "Get rec failed order record list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PagedResultDto<OrderRecordDto>()
        };
    }
    
    public async Task<FlowBehavior> HandleRequestReleaseExceptionAsync(Exception ex, GetRequestReleaseDto request)
    {
        _logger.LogError(ex, "Request release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
    
    public async Task<FlowBehavior> HandleRejectReleaseExceptionAsync(Exception ex, GetOrderOperationDto request)
    {
        _logger.LogError(ex, "Reject release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
    
    public async Task<FlowBehavior> HandleReleaseExceptionAsync(Exception ex, GetOrderSafeOperationDto request)
    {
        _logger.LogError(ex, "Release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
    
    public async Task<FlowBehavior> HandleRequestRefundExceptionAsync(Exception ex, GetRequestRefundDto request)
    {
        _logger.LogError(ex, "Request refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
    
    public async Task<FlowBehavior> HandleRejectRefundExceptionAsync(Exception ex, GetOrderOperationDto request)
    {
        _logger.LogError(ex, "Reject refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
    
    public async Task<FlowBehavior> HandleRefundExceptionAsync(Exception ex, GetOrderSafeOperationDto request)
    {
        _logger.LogError(ex, "Refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Throw,
            ReturnValue = new UserFriendlyException(ex.Message)
        };
    }
}