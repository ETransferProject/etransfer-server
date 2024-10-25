using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Info;

public partial class InfoAppService
{
    public async Task<FlowBehavior> HandleTxOverviewExceptionAsync(Exception ex, GetOverviewRequestDto request)
    {
        _logger.LogError(ex, "GetTransactionOverviewAsync error, type={type}", request.Type);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new GetTransactionOverviewResult()
        };
    }
    
    public async Task<FlowBehavior> HandleVolOverviewExceptionAsync(Exception ex, GetOverviewRequestDto request)
    {
        _logger.LogError(ex, "GetVolumeOverviewAsync error, type={type}", request.Type);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new GetVolumeOverviewResult()
        };
    }
    
    public async Task<FlowBehavior> HandleTokenExceptionAsync(Exception ex, GetTokenRequestDto request)
    {
        _logger.LogError(ex, "GetTokensAsync error, type={type}", request.Type);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new GetTokenResultDto()
        };
    }
    
    public async Task<FlowBehavior> HandleOptionExceptionAsync(Exception ex)
    {
        _logger.LogError(ex, "GetNetworkOptionAsync error.");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new GetTokenOptionResultDto()
        };
    }
    
    public async Task<FlowBehavior> HandleTransfersExceptionAsync(Exception ex, GetTransferRequestDto request)
    {
        _logger.LogError(ex,
            "GetTransfersAsync error, type={type}, fromToken={fromToken}, fromChainId={fromChainId}, toToken={toToken}, toChainId={toChainId}, ",
            request.Type, request.FromToken, request.FromChainId, request.ToToken, request.ToChainId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PagedResultDto<OrderIndexDto>()
        };
    }
}