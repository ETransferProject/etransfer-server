using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Service.Info;

public interface IInfoAppService : IApplicationService
{
     Task<GetTransactionOverviewResult> GetTransactionOverviewAsync(GetOverviewRequestDto request);
     Task<GetVolumeOverviewResult> GetVolumeOverviewAsync(GetOverviewRequestDto request);
     Task<GetTokenResultDto> GetTokensAsync(GetTokenRequestDto request);
     Task<GetTokenOptionResultDto> GetNetworkOptionAsync();
     Task<PagedResultDto<OrderIndexDto>> GetTransfersAsync(GetTransferRequestDto request);
}