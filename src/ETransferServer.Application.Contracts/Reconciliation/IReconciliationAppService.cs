using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Reconciliation;
using Volo.Abp.Application.Services;

namespace ETransferServer.Reconciliation;

public interface IReconciliationAppService : IApplicationService
{
    Task<GetTokenOptionResultDto> GetNetworkOptionAsync();
    Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request);
    Task<OrderDetailDto> GetOrderRecordDetailAsync(string id);
}