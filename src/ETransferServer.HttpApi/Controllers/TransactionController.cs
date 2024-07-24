using System.Threading.Tasks;
using ETransferServer.Dtos.Transaction;
using ETransferServer.Service.Transaction;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Transaction")]
[Route("api/etransfer/transaction")]
public class TransactionController : ETransferController
{
    private readonly ITransactionAppService _transactionAppService;

    public TransactionController(ITransactionAppService transactionAppService)
    {
        _transactionAppService = transactionAppService;
    }

    [HttpPost("callback")]
    public async Task<ContentResult> TransactionNotificationAsync([FromHeader(Name = "Biz-Timestamp")] string timestamp,
        [FromHeader(Name = "Biz-Resp-Signature")]
        string signature)
    {
        var result = await _transactionAppService.TransactionNotificationAsync(timestamp, signature);
        return Content(result);
    }
    
    [HttpGet("check")]
    public async Task<TransactionCheckResult> TransactionCheckAsync(GetTransactionCheckRequestDto request)
    {
        return await _transactionAppService.TransactionCheckAsync(request);
    }
}