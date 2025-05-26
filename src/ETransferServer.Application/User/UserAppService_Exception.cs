using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.User;
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
    
    public async Task<FlowBehavior> HandleCheckEoaRegistrationExceptionAsync(Exception ex, GetEoaRegistrationResultRequestDto requestDto)
    {
        Logger.LogError(ex, "Check eoa registration error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new EoaRegistrationResult
            {
                Result = false
            }
        };
    }
    
    public async Task<FlowBehavior> HandleCheckRegistrationExceptionAsync(Exception ex, GetRegistrationResultRequestDto requestDto)
    {
        Logger.LogError(ex, "Check registration error");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new RegistrationResult
            {
                Result = false
            }
        };
    }
}