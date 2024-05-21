using System.Threading.Tasks;

namespace ETransferServer.Swap;

public interface ITransactionTestAppService
{
    Task<long> GetTransactionTimeAsync(string network,string blockHash,string transactionId);
}