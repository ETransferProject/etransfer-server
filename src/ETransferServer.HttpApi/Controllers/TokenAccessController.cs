using System.Threading.Tasks;
using AElf.OpenTelemetry.ExecutionTime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.TokenAccess;
using Volo.Abp;

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
    [HttpPost("select-chain")]
    public async Task<SelectChainDto> SelectChainAsync(SelectChainInput input)
    {
        return await _tokenAccessAppService.SelectChainAsync(input);
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
    public async Task<TokenApplyOrderListDto> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input)
    {
        return await _tokenAccessAppService.GetTokenApplyOrderListAsync(input);
    }
    
    [Authorize]
    [HttpGet("{id}")]
    public async Task<TokenApplyOrderDto> GetTokenApplyOrderAsync(string id)
    {
        return await _tokenAccessAppService.GetTokenApplyOrderAsync(id);
    }
}