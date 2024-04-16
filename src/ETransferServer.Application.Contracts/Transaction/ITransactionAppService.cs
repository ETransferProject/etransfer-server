using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ETransferServer.Service.Transaction;

public interface ITransactionAppService : IApplicationService
{
     Task<string> TransactionNotificationAsync(string timeStamp, string signature, string body);
}