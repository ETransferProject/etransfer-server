using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Reconciliation;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

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
    
    [Authorize(Roles = "superAdmin")]
    [HttpPost("init")]
    public virtual async Task<bool> InitUserAsync(GetUserDto request)
    {
        return await _reconciliationAppService.InitUserAsync(request);
    }
    
    [Authorize]
    [HttpGet("record/{id}")]
    public async Task<OrderMoreDetailDto> GetOrderRecordDetailAsync(string id)
    {
        return await _reconciliationAppService.GetOrderRecordDetailAsync(id);
    }
    
    [Authorize]
    [HttpGet("deposit-order")]
    public async Task<OrderPagedResultDto<OrderRecordDto>> GetDepositOrderRecordListAsync(GetOrderRequestDto request)
    {
        return await _reconciliationAppService.GetDepositOrderRecordListAsync(request);
    }
    
    [Authorize]
    [HttpGet("withdraw-order")]
    public async Task<OrderPagedResultDto<OrderRecordDto>> GetWithdrawOrderRecordListAsync(GetOrderRequestDto request)
    {
        return await _reconciliationAppService.GetWithdrawOrderRecordListAsync(request);
    }
    
    [Authorize]
    [HttpGet("fail-order")]
    public async Task<PagedResultDto<OrderRecordDto>> GetFailOrderRecordListAsync(GetOrderRequestDto request)
    {
        return await _reconciliationAppService.GetFailOrderRecordListAsync(request);
    }
    
    [Authorize(Roles = "ordinary")]
    [HttpPost("release-request")]
    public async Task<OrderOperationStatusDto> RequestReleaseTokenAsync(GetRequestReleaseDto request)
    {
        return await _reconciliationAppService.RequestReleaseTokenAsync(request);
    }
    
    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("release-reject")]
    public async Task<OrderOperationStatusDto> RejectReleaseTokenAsync(GetOrderOperationDto request)
    {
        return await _reconciliationAppService.RejectReleaseTokenAsync(request);
    }
    
    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("release")]
    public async Task<OrderOperationStatusDto> ReleaseTokenAsync(GetOrderSafeOperationDto request)
    {
        return await _reconciliationAppService.ReleaseTokenAsync(request);
    }
    
    [Authorize(Roles = "ordinary")]
    [HttpPost("refund-request")]
    public async Task<OrderOperationStatusDto> RequestRefundTokenAsync(GetRequestRefundDto request)
    {
        return await _reconciliationAppService.RequestRefundTokenAsync(request);
    }
    
    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("refund-reject")]
    public async Task<OrderOperationStatusDto> RejectRefundTokenAsync(GetOrderOperationDto request)
    {
        return await _reconciliationAppService.RejectRefundTokenAsync(request);
    }
    
    [Authorize(Roles = "superAdmin,admin")]
    [HttpPost("refund")]
    public async Task<OrderOperationStatusDto> RefundTokenAsync(GetOrderSafeOperationDto request)
    {
        return await _reconciliationAppService.RefundTokenAsync(request);
    }
}