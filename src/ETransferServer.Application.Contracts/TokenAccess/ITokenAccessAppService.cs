using System.Threading.Tasks;
using ETransferServer.Dtos.TokenAccess;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.TokenAccess;

public interface ITokenAccessAppService
{
    Task<AvailableTokensDto> GetAvailableTokensAsync();
    Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input);
    Task AddOrUpdateUserTokenAccessInfoAsync(UserTokenAccessInfoDto dto);
    Task AddOrUpdateUserTokenApplyOrderAsync(TokenApplyOrderDto dto);
    Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input);
    Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input);
    Task<AddChainResultDto> AddChainAsync(AddChainInput input);
    Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input);
    Task<bool> GetBindingIssueAsync(string id);
    Task<PagedResultDto<TokenApplyOrderDto>> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input);
    Task<TokenApplyOrderDto> GetTokenApplyOrderDetailAsync(GetTokenApplyOrderInput input);
}