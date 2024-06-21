using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Models;
using ETransferServer.token;
using ETransferServer.token.Dtos;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Token")]
[Route("api/etransfer/token")]
public class TokenController : ETransferController
{
    private readonly ITokenAppService _tokenAppService;

    public TokenController(ITokenAppService tokenAppService)
    {
        _tokenAppService = tokenAppService;
    }

    [HttpGet("list")]
    public async Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request)
    {
        return await _tokenAppService.GetTokenListAsync(request);
    }
    
    [HttpGet("option")]
    public async Task<GetTokenOptionListDto> GetTokenListAsync(GetTokenOptionListRequestDto request)
    {
        return await _tokenAppService.GetTokenOptionListAsync(request);
    }
}