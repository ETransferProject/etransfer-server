using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.token;
using ETransferServer.token.Dtos;
using ETransferServer.Token.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Token")]
[Route("api/etransfer/token")]
public class TokenController : ETransferController
{
    private readonly ITokenAppService _tokenAppService;
    private readonly INetworkAppService _networkAppService;

    public TokenController(ITokenAppService tokenAppService,
        INetworkAppService networkAppService)
    {
        _tokenAppService = tokenAppService;
        _networkAppService = networkAppService;
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
    
    [HttpGet("price")]
    public async Task<ListResultDto<TokenPriceDataDto>> GetTokenPriceListAsync(GetTokenPriceListRequestDto request)
    {
        return await _networkAppService.GetTokenPriceListAsync(request);
    }
}