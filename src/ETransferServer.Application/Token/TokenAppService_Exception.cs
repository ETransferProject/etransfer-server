using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Models;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Token;

public partial class TokenAppService
{
    public async Task<FlowBehavior> HandleListExceptionAsync(Exception ex, GetTokenListRequestDto request)
    {
        _logger.LogError(ex, "GetTokenList error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
    
    public async Task<FlowBehavior> HandleOptionExceptionAsync(Exception ex, GetTokenOptionListRequestDto request)
    {
        _logger.LogError(ex, "GetTokenOptionList error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
}