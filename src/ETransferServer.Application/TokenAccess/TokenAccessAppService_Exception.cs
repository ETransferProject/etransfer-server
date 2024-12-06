using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.TokenAccess;
using Microsoft.Extensions.Logging;

namespace ETransferServer.TokenAccess;

public partial class TokenAccessAppService
{
    public async Task<FlowBehavior> HandleAddOrUpdateUserTokenAccessInfoExceptionAsync(Exception ex, UserTokenAccessInfoDto dto)
    {
        Logger.LogError(ex, "Save token access info error, userId:{userId}, symbol:{symbol}", dto.Symbol);
        return new FlowBehavior();
    }
}