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

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Order")]
[Route("api/app/")]
[IgnoreAntiforgeryToken]
public class OrderController : ETransferController
{
    private readonly IOrderWithdrawAppService _withdrawOrderAppService;
    private readonly IOrderDepositAppService _depositOrderAppService;
    private readonly INetworkAppService _networkAppService;

    public OrderController(IOrderWithdrawAppService withdrawOrderAppService,
        IOrderDepositAppService depositOrderAppService,INetworkAppService networkAppService)
    {
        _withdrawOrderAppService = withdrawOrderAppService;
        _depositOrderAppService = depositOrderAppService;
        _networkAppService = networkAppService;
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

    [Authorize]
    [HttpGet("withdraw/info")]
    public async Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request)
    {
        return await _withdrawOrderAppService.GetWithdrawInfoAsync(request);
    }
     
    [Authorize]
    [HttpPost("withdraw/order")]
    public async Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(GetWithdrawOrderRequestDto request)
    {
        return await _withdrawOrderAppService.CreateWithdrawOrderInfoAsync(request);
    }
}