using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.User.Dtos;
using Microsoft.Extensions.Logging;

namespace ETransferServer.User;

public partial class UserAppService
{
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, UserDto user)
    {
        Logger.LogError(ex, "Create user error, userId:{userId}, appId:{appId}", user.UserId, user.AppId);
        return new FlowBehavior();
    }
}