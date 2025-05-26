using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Dtos.Transaction;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Service.Transaction;

public partial class TransactionAppService
{
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, string timestamp, string signature)
    {
        _logger.LogError(ex, "handle receive transaction callback error, timestamp:{timestamp}, signature:{signature}",
            timestamp, signature);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = NotificationEnum.Deny.ToString().ToLower()
        };
    }
    
    public async Task<FlowBehavior> HandleTransactionCheckExceptionAsync(Exception ex, GetTransactionCheckRequestDto request)
    {
        _logger.LogError(ex, "handle transaction check error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new TransactionCheckResult
            {
                Result = false
            }
        };
    }
}