using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Reconciliation;
using Microsoft.Extensions.Logging;
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
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRejectReleaseExceptionAsync(Exception ex, GetOrderOperationDto request)
    {
        _logger.LogError(ex, "Reject release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleReleaseExceptionAsync(Exception ex, GetOrderSafeOperationDto request)
    {
        _logger.LogError(ex, "Release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRequestRefundExceptionAsync(Exception ex, GetRequestRefundDto request)
    {
        _logger.LogError(ex, "Request refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRejectRefundExceptionAsync(Exception ex, GetOrderOperationDto request)
    {
        _logger.LogError(ex, "Reject refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRefundExceptionAsync(Exception ex, GetOrderSafeOperationDto request)
    {
        _logger.LogError(ex, "Refund token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRequestTransferReleaseExceptionAsync(Exception ex, GetRequestReleaseDto request)
    {
        _logger.LogError(ex, "Request transfer release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleRejectTransferReleaseExceptionAsync(Exception ex, GetOrderOperationDto request)
    {
        _logger.LogError(ex, "Reject transfer release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleTransferReleaseExceptionAsync(Exception ex, GetOrderSafeOperationDto request)
    {
        _logger.LogError(ex, "Transfer release token failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleGetPoolOverviewExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get pool overview failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PoolOverviewListDto()
        };
    }
    
    public async Task<FlowBehavior> HandleResetPoolInitExceptionAsync(Exception ex, GetPoolRequestDto request)
    {
        _logger.LogError(ex, "Reset pool init failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetPoolChangeListExceptionAsync(Exception ex, PagedAndSortedResultRequestDto request)
    {
        _logger.LogError(ex, "Get pool change list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PoolChangeListDto<PoolChangeDto> {
                TotalCount = 0,
                Pool = new Dictionary<string, List<PoolChangeDto>>()
            }
        };
    }
    
    public async Task<FlowBehavior> HandleGetMultiPoolOverviewExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get multi pool overview failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new MultiPoolOverviewDto()
        };
    }
    
    public async Task<FlowBehavior> HandleResetMultiPoolThresholdExceptionAsync(Exception ex, GetMultiPoolRequestDto request)
    {
        _logger.LogError(ex, "Reset multi pool threshold failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetMultiPoolChangeListExceptionAsync(Exception ex, PagedAndSortedResultRequestDto request)
    {
        _logger.LogError(ex, "Get multi pool change list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new MultiPoolChangeListDto<MultiPoolChangeDto> {
                TotalCount = 0,
                MultiPool = new Dictionary<string, List<MultiPoolChangeDto>>()
            }
        };
    }
    
    public async Task<FlowBehavior> HandleGetTokenPoolOverviewExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get token pool overview failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new TokenPoolOverviewDto()
        };
    }
    
    public async Task<FlowBehavior> HandleResetTokenPoolThresholdExceptionAsync(Exception ex, GetTokenPoolRequestDto request)
    {
        _logger.LogError(ex, "Reset token pool threshold failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetTokenPoolChangeListExceptionAsync(Exception ex, PagedAndSortedResultRequestDto request)
    {
        _logger.LogError(ex, "Get token pool change list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new TokenPoolChangeListDto<TokenPoolChangeDto> {
                TotalCount = 0,
                TokenPool = new Dictionary<string, List<TokenPoolChangeDto>>()
            }
        };
    }
    
    public async Task<FlowBehavior> HandleGetFeeOverviewExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "Get fee overview failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new FeeOverviewDto()
        };
    }
    
    public async Task<FlowBehavior> HandleResetFeeInitExceptionAsync(Exception ex, GetFeeRequestDto request)
    {
        _logger.LogError(ex, "Reset fee init failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetFeeChangeListExceptionAsync(Exception ex, PagedAndSortedResultRequestDto request)
    {
        _logger.LogError(ex, "Get fee change list failed");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new Dictionary<string, Dictionary<string, List<FeeChangeDto>>>()
        };
    }
}