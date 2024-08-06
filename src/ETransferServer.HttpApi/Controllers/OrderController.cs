using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ETransferServer.Deposit.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.Network.Dtos;
using ETransferServer.Order;
using ETransferServer.Withdraw.Dtos;
using ETransferServer.WithdrawOrder.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Order")]
[Route("api/etransfer/")]
[IgnoreAntiforgeryToken]
public class OrderController : ETransferController
{
    private readonly IOrderWithdrawAppService _withdrawOrderAppService;
    private readonly IOrderDepositAppService _depositOrderAppService;
    private readonly INetworkAppService _networkAppService;
    private readonly IOrderAppService _orderAppService;

    public OrderController(IOrderWithdrawAppService withdrawOrderAppService,
        IOrderDepositAppService depositOrderAppService,
        INetworkAppService networkAppService,
        IOrderAppService orderAppService)
    {
        _withdrawOrderAppService = withdrawOrderAppService;
        _depositOrderAppService = depositOrderAppService;
        _networkAppService = networkAppService;
        _orderAppService = orderAppService;
    }
    
    [HttpGet("network/list")]
    public async Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request)
    {
        return await _networkAppService.GetNetworkListAsync(request);
    }

    [Authorize]
    [HttpGet("deposit/info")]
    public async Task<GetDepositInfoDto> GetDepositInfoAsync(GetDepositRequestDto request)
    {
        return await _depositOrderAppService.GetDepositInfoAsync(request);
    }
    
    [HttpGet("deposit/calculator")]
    public async Task<CalculateDepositRateDto> CalculateDepositRateAsync(GetCalculateDepositRateRequestDto request)
    {
        return await _depositOrderAppService.CalculateDepositRateAsync(request);
    }

    [Authorize]
    [HttpGet("withdraw/info")]
    public async Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request)
    {
        return await _withdrawOrderAppService.GetWithdrawInfoAsync(request);
    }
     
    [Authorize]
    [HttpPost("withdraw/order")]
    public async Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(
        [FromHeader(Name = "version")] string version, GetWithdrawOrderRequestDto request)
    {
        return await _withdrawOrderAppService.CreateWithdrawOrderInfoAsync(version, request);
    }

    [Authorize]
    [HttpGet("record/list")]
    public async Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request)
    {
        return await _orderAppService.GetOrderRecordListAsync(request);
    }

    [Authorize]
    [HttpGet("record/status")]
    public async Task<OrderStatusDto> GetOrderRecordStatusAsync()
    {
        return await _orderAppService.GetOrderRecordStatusAsync();
    }
}