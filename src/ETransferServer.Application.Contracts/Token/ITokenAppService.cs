using System.Threading.Tasks;
using ETransferServer.Models;
using ETransferServer.token.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.token;

public interface ITokenAppService : IApplicationService
{
    Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request);
}