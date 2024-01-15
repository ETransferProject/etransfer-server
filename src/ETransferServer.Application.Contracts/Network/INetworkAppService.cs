using System.Threading.Tasks;
using ETransferServer.Models;
using ETransferServer.Network.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Network;

public interface INetworkAppService : IApplicationService
{
    Task<GetNetworkListDto> GetNetworkListAsync(GetNetworkListRequestDto request);
}