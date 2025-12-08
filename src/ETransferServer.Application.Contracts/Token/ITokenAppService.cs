using System.Threading.Tasks;
using ETransferServer.Models;
using ETransferServer.Token.Dtos;
using JetBrains.Annotations;
using Volo.Abp.Application.Services;

namespace ETransferServer.Token;

public interface ITokenAppService : IApplicationService
{
    Task<GetTokenListDto> GetTokenListAsync(GetTokenListRequestDto request);
    Task<GetTokenOptionListDto> GetTokenOptionListAsync(GetTokenOptionListRequestDto request);
    bool IsValidDeposit(string toChainId, string fromSymbol, [CanBeNull] string toSymbol);
    bool IsValidSwap(string toChainId, string fromSymbol, [CanBeNull] string toSymbol);
}