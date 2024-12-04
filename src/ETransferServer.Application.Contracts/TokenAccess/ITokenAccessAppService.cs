using System.Threading.Tasks;
using ETransferServer.Dtos.TokenAccess;

namespace ETransferServer.TokenAccess;

public interface ITokenAccessAppService
{
    Task<AvailableTokensDto> GetAvailableTokensAsync();
    Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input);
    Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input);
    Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input);
    Task<SelectChainDto> SelectChainAsync(SelectChainInput input);
    Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input);
    Task<bool> GetBindingIssueAsync(string id);
    Task<TokenApplyOrderListDto> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input);
    Task<TokenApplyOrderDto> GetTokenApplyOrderAsync(string id);
}