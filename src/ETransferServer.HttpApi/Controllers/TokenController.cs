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
[Route("api/app/token")]
public class TokenController : ETransferController
{
    private readonly ITokenAppService _tokenAppService;

    public TokenController(ITokenAppService tokenAppService)
    {
        _tokenAppService = tokenAppService;
    }

    [HttpGet("list")]
    public async Task<GetTokenListDto> ReceiveAsync( GetTokenListRequestDto request)
    {
        return await _tokenAppService.GetTokenListAsync(request);
    }
}