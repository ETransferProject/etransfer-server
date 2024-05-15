using System.Threading.Tasks;
using ETransferServer.Swap;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;
[RemoteService]
[Area("app")]
[ControllerName("EvmTransaction")]
[Route("api/app/EvmTransaction")]
[IgnoreAntiforgeryToken]
public class EvmTransactionController : ETransferController
{
    private readonly ITransactionTestAppService _transactionTestAppService;
    public EvmTransactionController(ITransactionTestAppService transactionTestAppService)
    {
        _transactionTestAppService = transactionTestAppService;
    }
    [HttpPost("test")]
    public async Task<long> TestSwap(string network,string blockHash,string txId)
    {
        var result = await _transactionTestAppService.GetTransactionTimeAsync(network,blockHash,txId);
        return result;
    }
}