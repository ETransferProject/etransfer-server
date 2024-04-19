using System.Threading.Tasks;
using ETransferServer.Service.Transaction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("Transaction")]
[Route("api/app/transaction")]
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
}