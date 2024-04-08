using System.Threading.Tasks;
using ETransferServer.Cobo;
using ETransferServer.Network.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Cobo")]
[Route("api/app/")]
[IgnoreAntiforgeryToken]
public class CoboController : ETransferController
{
    private readonly ICoboAppService _coboAppService;
    public CoboController(ICoboAppService coboAppService)
    {
        _coboAppService = coboAppService;
    }
    
    [HttpPost("custody_callback/")]
    public async Task<TransactionNotificationResponse> TransactionNotification([FromHeader(Name = "Biz-Timestamp")] long timestamp,
        [FromHeader(Name = "Biz-Resp-Signature")] string signature,
        [FromBody] string body)
    {
        return await _coboAppService.TransactionNotificationAsync(timestamp, signature, body);
    }
}