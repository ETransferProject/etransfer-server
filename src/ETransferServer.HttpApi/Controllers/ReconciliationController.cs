using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Reconciliation;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Reconciliation")]
[Route("api/reconciliation/")]
public class ReconciliationController : ETransferController
{
    private readonly IReconciliationAppService _reconciliationAppService;
    
    public ReconciliationController(IReconciliationAppService reconciliationAppService)
    {
        _reconciliationAppService = reconciliationAppService;
    }

    [Authorize]
    [HttpGet("network/option")]
    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        return await _reconciliationAppService.GetNetworkOptionAsync();
    }
    
    [Authorize]
    [HttpPost("change-password")]
    public virtual async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request)
    {
        return await _reconciliationAppService.ChangePasswordAsync(request);
    }
    
    [Authorize]
    [HttpGet("record/{id}")]
    public async Task<OrderDetailDto> GetOrderRecordDetailAsync(string id)
    {
        return await _reconciliationAppService.GetOrderRecordDetailAsync(id);
    }
}