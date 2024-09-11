using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using Volo.Abp.Application.Services;

namespace ETransferServer.Reconciliation;

public interface IReconciliationAppService : IApplicationService
{
    Task<GetTokenOptionResultDto> GetNetworkOptionAsync();
    Task<OrderDetailDto> GetOrderRecordDetailAsync(string id);
}