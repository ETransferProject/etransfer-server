using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Service.Info;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Info")]
[Route("api/app/info")]
public class InfoController : ETransferController
{
    private readonly IInfoAppService _infoAppService;

    public InfoController(IInfoAppService infoAppService)
    {
        _infoAppService = infoAppService;
    }

    [HttpGet("transaction-overview")]
    public async Task<GetTransactionOverviewResult> GetTransactionOverviewAsync(GetOverviewRequestDto request)
    {
        return await _infoAppService.GetTransactionOverviewAsync(request);
    }
    
    [HttpGet("volume-overview")]
    public async Task<GetVolumeOverviewResult> GetVolumeOverviewAsync(GetOverviewRequestDto request)
    {
        return await _infoAppService.GetVolumeOverviewAsync(request);
    }
    
    [HttpGet("tokens")]
    public async Task<GetTokenResultDto> GetTokensAsync(GetTokenRequestDto request)
    {
        return await _infoAppService.GetTokensAsync(request);
    }
    
    [HttpGet("network/option")]
    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        return await _infoAppService.GetNetworkOptionAsync();
    }
    
    [HttpGet("transfers")]
    public async Task<PagedResultDto<OrderIndexDto>> GetTransfersAsync(GetTransferRequestDto request)
    {
        return await _infoAppService.GetTransfersAsync(request);
    }
}