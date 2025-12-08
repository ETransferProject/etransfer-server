using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
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
}