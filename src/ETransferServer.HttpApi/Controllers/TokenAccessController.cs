using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.OpenTelemetry.ExecutionTime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.TokenAccess;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Application")]
[Route("api/etransfer/application/")]
[AggregateExecutionTime]
public class TokenAccessController : ETransferController
{
    private readonly ITokenAccessAppService _tokenAccessAppService;
    
    public TokenAccessController(ITokenAccessAppService tokenAccessAppService)
    {
        _tokenAccessAppService = tokenAccessAppService;
    }
    
    [HttpGet("token/config")]
    public async Task<TokenConfigDto> GetTokenConfigAsync(GetTokenConfigInput input)
    {
        return await _tokenAccessAppService.GetTokenConfigAsync(input);
    }

    [Authorize]
    [HttpGet("tokens")]
    public async Task<AvailableTokensDto> GetAvailableTokensAsync()
    {
        return await _tokenAccessAppService.GetAvailableTokensAsync();
    }
    
    [Authorize]
    [HttpPost("commit-basic-info")]
    public async Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input)
    {
        return await _tokenAccessAppService.CommitTokenAccessInfoAsync(input);
    }
    
    [Authorize]
    [HttpGet("user-token-access-info")]
    public async Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input)
    {
        return await _tokenAccessAppService.GetUserTokenAccessInfoAsync(input);
    }
    
    [Authorize]
    [HttpGet("check-chain-access-status")]
    public async Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input)
    {
        return await _tokenAccessAppService.CheckChainAccessStatusAsync(input);
    }
    
    [Authorize]
    [HttpPost("add-chain")]
    public async Task<AddChainResultDto> AddChainAsync(AddChainInput input)
    {
        return await _tokenAccessAppService.AddChainAsync(input);
    }
    
    [Authorize]
    [HttpPost("prepare-binding-issue")]
    public async Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input)
    {
        return await _tokenAccessAppService.PrepareBindingIssueAsync(input);
    }
    
    [Authorize]
    [HttpGet("issue/{id}")]
    public async Task<bool> GetBindingIssueAsync(string id)
    {
        return await _tokenAccessAppService.GetBindingIssueAsync(id);
    }
    
    [Authorize]
    [HttpGet("list")]
    public async Task<PagedResultDto<TokenApplyOrderResultDto>> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input)
    {
        return await _tokenAccessAppService.GetTokenApplyOrderListAsync(input);
    }
    
    [Authorize]
    [HttpGet("detail")]
    public async Task<List<TokenApplyOrderResultDto>> GetTokenApplyOrderDetailAsync(GetTokenApplyOrderInput input)
    {
        return await _tokenAccessAppService.GetTokenApplyOrderDetailAsync(input);
    }
}