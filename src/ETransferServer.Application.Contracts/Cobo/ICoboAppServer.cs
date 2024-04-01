using System.Threading.Tasks;
using ETransferServer.Network.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Cobo;

public interface ICoboAppService : IApplicationService
{
     Task<TransactionNotificationResponse> TransactionNotificationAsync(string timeStamp, string signature, string body);
}