using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Dtos.TokenAccess;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.TokenAccess;

public partial class TokenAccessAppService
{
    public async Task<FlowBehavior> HandleGetAvailableTokensExceptionAsync(Exception ex)
    {
        Logger.LogError(ex, "GetAvailableTokensAsync error.");
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new AvailableTokensDto()
        };
    }
    
    public async Task<FlowBehavior> HandleCommitTokenAccessInfoExceptionAsync(Exception ex, UserTokenAccessInfoInput dto)
    {
        Logger.LogError(ex, "CommitTokenAccessInfoAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleAddOrUpdateUserTokenAccessInfoExceptionAsync(Exception ex, UserTokenAccessInfoDto dto)
    {
        Logger.LogError(ex, "Save token access info error, address:{address}, symbol:{symbol}", 
            dto.UserAddress, dto.Symbol);
        return new FlowBehavior();
    }
    
    public async Task<FlowBehavior> HandleAddOrUpdateUserTokenApplyOrderExceptionAsync(Exception ex, TokenApplyOrderDto dto)
    {
        Logger.LogError(ex, "Save token apply order error, address:{address}, symbol:{symbol}", 
            dto.UserAddress, dto.Symbol);
        return new FlowBehavior();
    }
    
    public async Task<FlowBehavior> HandleGetUserTokenAccessInfoExceptionAsync(Exception ex, UserTokenAccessInfoBaseInput dto)
    {
        Logger.LogError(ex, "GetUserTokenAccessInfoAsync error, {symbol}", dto.Symbol);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new UserTokenAccessInfoDto()
        };
    }
    
    public async Task<FlowBehavior> HandleCheckChainAccessStatusExceptionAsync(Exception ex, CheckChainAccessStatusInput dto)
    {
        Logger.LogError(ex, "CheckChainAccessStatusAsync error, {symbol}", dto.Symbol);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new CheckChainAccessStatusResultDto()
        };
    }
    
    public async Task<FlowBehavior> HandleAddChainExceptionAsync(Exception ex, AddChainInput dto)
    {
        Logger.LogError(ex, "AddChainAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new AddChainResultDto()
        };
    }
    
    public async Task<FlowBehavior> HandlePrepareBindingIssueExceptionAsync(Exception ex, PrepareBindIssueInput dto)
    {
        Logger.LogError(ex, "PrepareBindingIssueAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new UserTokenBindingDto()
        };
    }
    
    public async Task<FlowBehavior> HandleGetBindingIssueExceptionAsync(Exception ex, UserTokenBindingDto dto)
    {
        Logger.LogError(ex, "GetBindingIssueAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleChangeStatusExceptionAsync(Exception ex, GetTokenApplyOrderInput dto)
    {
        Logger.LogError(ex, "ChangeStatusAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = false
        };
    }
    
    public async Task<FlowBehavior> HandleGetTokenApplyOrderListExceptionAsync(Exception ex, GetTokenApplyOrderListInput dto)
    {
        Logger.LogError(ex, "GetTokenApplyOrderListAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new PagedResultDto<TokenApplyOrderResultDto>()
        };
    }
    
    public async Task<FlowBehavior> HandleGetTokenApplyOrderDetailExceptionAsync(Exception ex, GetTokenApplyOrderInput dto)
    {
        Logger.LogError(ex, "GetTokenApplyOrderDetailAsync error, {dto}", JsonConvert.SerializeObject(dto));
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new List<TokenApplyOrderResultDto>()
        };
    }
}