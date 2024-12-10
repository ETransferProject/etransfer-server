using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.TokenAccess;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.TokenAccess;

public interface ITokenAccessAppService
{
    Task<TokenConfigDto> GetTokenConfigAsync(GetTokenConfigInput input);
    Task<AvailableTokensDto> GetAvailableTokensAsync();
    Task<bool> CommitTokenAccessInfoAsync(UserTokenAccessInfoInput input);
    Task AddOrUpdateUserTokenAccessInfoAsync(UserTokenAccessInfoDto dto);
    Task AddOrUpdateUserTokenApplyOrderAsync(TokenApplyOrderDto dto);
    Task<UserTokenAccessInfoDto> GetUserTokenAccessInfoAsync(UserTokenAccessInfoBaseInput input);
    Task<CheckChainAccessStatusResultDto> CheckChainAccessStatusAsync(CheckChainAccessStatusInput input);
    Task<AddChainResultDto> AddChainAsync(AddChainInput input);
    Task<UserTokenBindingDto> PrepareBindingIssueAsync(PrepareBindIssueInput input);
    Task<bool> GetBindingIssueAsync(UserTokenBindingDto input);
    Task<PagedResultDto<TokenApplyOrderResultDto>> GetTokenApplyOrderListAsync(GetTokenApplyOrderListInput input);
    Task<List<TokenApplyOrderResultDto>> GetTokenApplyOrderDetailAsync(GetTokenApplyOrderInput input);
}