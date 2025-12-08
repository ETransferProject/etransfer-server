using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Models;
using Microsoft.Extensions.Logging;
using Volo.Abp;

namespace ETransferServer.Network;

public partial class NetworkAppService
{
    public async Task<FlowBehavior> HandleGetListExceptionAsync(Exception ex, GetNetworkListRequestDto request, 
        string version = null)
    {
        if (ex is UserFriendlyException)
        {
            _logger.LogWarning(ex, "Get network list failed.");
        }
        else
        {
            _logger.LogError(ex,
                "Get network list failed, type={Type}, chainId={ChainId}, address={Address}, symbol={Symbol}",
                request.Type, request.ChainId, request.Address, request.Symbol);
        }
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Rethrow
        };
    }
}