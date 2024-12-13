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
    Task DoLiquidityMonitor(string symbol, string address, string liquidityInUsd);
}

public class UserTokenAccessMonitorGrain : Grain<UserTokenAccessMonitorState>, IUserTokenAccessMonitorGrain
{
    private const string TokenListingAlarm = "TokenListingAlarm";
    private const string LiquidityInsufficientAlarm = "LiquidityInsufficientAlarm";
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

    public async Task DoLiquidityMonitor(string symbol, string address, string liquidityInUsd)
    {
        try
        {
            var userTokenAccessInfoGrain = GrainFactory.GetGrain<IUserTokenAccessInfoGrain>(
                string.Join(CommonConstant.Underline, symbol, address));
            var userTokenAccessInfo = await userTokenAccessInfoGrain.Get();
                    
            var liquidityDto = new LiquidityDto
            {
                Token = symbol,
                LiquidityInUsd = liquidityInUsd,
                Chain = ChainId.AELF,
                PersonName = userTokenAccessInfo?.PersonName,
                TelegramHandler = userTokenAccessInfo?.TelegramHandler,
                Email = userTokenAccessInfo?.Email
            };
            await SendNotifyAsync(liquidityDto);
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning(
                "LiquidityInsufficientMonitor handle failed , Message={Msg}, GrainId={GrainId} liquidityInUsd={usd}",
                e.Message, this.GetPrimaryKeyString(), liquidityInUsd);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "LiquidityInsufficientMonitor handle failed GrainId={GrainId}, " +
                "liquidityInUsd={usd}", this.GetPrimaryKeyString(), liquidityInUsd);
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
    
    private async Task<bool> SendNotifyAsync(LiquidityDto dto)
    {
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        return await provider.SendNotifyAsync(new NotifyRequest
        {
            Template = LiquidityInsufficientAlarm,
            Params = new Dictionary<string, string>
            {
                [LiquidityKeys.Token] = dto.Token,
                [LiquidityKeys.LiquidityInUsd] = dto.LiquidityInUsd,
                [LiquidityKeys.Chain] = dto.Chain,
                [LiquidityKeys.PersonName] = dto.PersonName,
                [LiquidityKeys.TelegramHandler] = dto.TelegramHandler,
                [LiquidityKeys.Email] = dto.Email
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
    
    public class LiquidityDto
    {
        public string Token { get; set; }
        public string LiquidityInUsd { get; set; }
        public string Chain { get; set; }
        public string PersonName { get; set; }
        public string TelegramHandler { get; set; }
        public string Email { get; set; }
    }

    private static class TokenListingKeys
    {
        public const string Token = "token";
        public const string TokenContract = "tokenContract";
        public const string Chain = "chain";
        public const string Website = "website";
    }
    
    private static class LiquidityKeys
    {
        public const string Token = "token";
        public const string LiquidityInUsd = "liquidityInUsd";
        public const string Chain = "chain";
        public const string PersonName = "personName";
        public const string TelegramHandler = "telegramHandler";
        public const string Email = "email";
    }
}