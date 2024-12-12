using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.Grains.State.Users;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenAccessMonitorGrain : IGrainWithStringKey
{
    Task DoTokenListingMonitor(AddChainResultDto dto);
}

public class UserTokenAccessMonitorGrain : Grain<UserTokenAccessMonitorState>, IUserTokenAccessMonitorGrain
{
    private const string TokenListingAlarm = "TokenListingAlarm";
    private readonly ILogger<WithdrawOrderMonitorGrain> _logger;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;

    public UserTokenAccessMonitorGrain(ILogger<WithdrawOrderMonitorGrain> logger, 
        IEnumerable<INotifyProvider> notifyProvider)
    {
        _logger = logger;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }
    
    public async Task DoTokenListingMonitor(AddChainResultDto dto)
    {
        try
        {
            if (!dto.ChainList.IsNullOrEmpty())
            {
                foreach (var item in dto.ChainList)
                {
                    var tokenApplyOrderGrain = GrainFactory.GetGrain<IUserTokenApplyOrderGrain>(Guid.Parse(item.Id));
                    var tokenApplyOrderDto = await tokenApplyOrderGrain.Get();
                    
                    if (tokenApplyOrderDto == null) continue;
                    var userTokenAccessInfoGrain = GrainFactory.GetGrain<IUserTokenAccessInfoGrain>(
                    string.Join(CommonConstant.Underline, tokenApplyOrderDto.Symbol, tokenApplyOrderDto.UserAddress));
                    var userTokenAccessInfo = await userTokenAccessInfoGrain.Get();
                    
                    var tokenListingDto = new TokenListingDto
                    {
                        Token = tokenApplyOrderDto.Symbol,
                        TokenContract = tokenApplyOrderDto.ChainTokenInfo[0]?.ContractAddress,
                        Chain = item.ChainId,
                        Website = userTokenAccessInfo?.OfficialWebsite
                    };
                    await SendNotifyAsync(tokenListingDto);
                }
            }

            if (!dto.OtherChainList.IsNullOrEmpty())
            {
                foreach (var item in dto.OtherChainList)
                {
                    var tokenApplyOrderGrain = GrainFactory.GetGrain<IUserTokenApplyOrderGrain>(Guid.Parse(item.Id));
                    var tokenApplyOrderDto = await tokenApplyOrderGrain.Get();

                    if (tokenApplyOrderDto == null) continue;
                    var userTokenAccessInfoGrain = GrainFactory.GetGrain<IUserTokenAccessInfoGrain>(
                        string.Join(CommonConstant.Underline, tokenApplyOrderDto.Symbol, tokenApplyOrderDto.UserAddress));
                    var userTokenAccessInfo = await userTokenAccessInfoGrain.Get();
                    
                    var tokenListingDto = new TokenListingDto
                    {
                        Token = tokenApplyOrderDto.Symbol,
                        TokenContract = tokenApplyOrderDto.OtherChainTokenInfo?.ContractAddress,
                        Chain = item.ChainId,
                        Website = userTokenAccessInfo?.OfficialWebsite
                    };
                    await SendNotifyAsync(tokenListingDto);
                }
            }
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "TokenListingMonitor handle failed , Message={Msg}, GrainId={GrainId} dto={dto}",
                e.Message, this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TokenListingMonitor handle failed GrainId={GrainId}, dto={dto}",
                this.GetPrimaryKeyString(), JsonConvert.SerializeObject(dto));
        }
    }
    
    private async Task<bool> SendNotifyAsync(TokenListingDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = TokenListingAlarm,
            Params = new Dictionary<string, string>
            {
                [TokenListingKeys.Token] = dto.Token,
                [TokenListingKeys.TokenContract] = dto.TokenContract,
                [TokenListingKeys.Chain] = dto.Chain,
                [TokenListingKeys.Website] = dto.Website
            }
        });
    }

    public class TokenListingDto
    {
        public string Token { get; set; }
        public string TokenContract { get; set; }
        public string Chain { get; set; }
        public string Website { get; set; }
    }

    private static class TokenListingKeys
    {
        public const string Token = "token";
        public const string TokenContract = "tokenContract";
        public const string Chain = "chain";
        public const string Website = "website";
    }
}