using System.Threading.Tasks;
using ETransferServer.Dtos.Transaction;
using Volo.Abp.Application.Services;

namespace ETransferServer.Service.Transaction;

public interface ITransactionAppService : IApplicationService
{
     Task<string> TransactionNotificationAsync(string timeStamp, string signature);
     Task<TransactionCheckResult> TransactionCheckAsync(GetTransactionCheckRequestDto request);
}