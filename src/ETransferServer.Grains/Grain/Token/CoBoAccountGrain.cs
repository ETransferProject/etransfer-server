using ETransferServer.Common;
using ETransferServer.Grains.State.Token;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ICoBoAccountGrain : IGrainWithGuidKey
{
    public static string Id(string network, string symbol)
    {
        return string.Join(CommonConstant.Underline, network, symbol);
    }
    
    Task<AssetDto> Get(string coin);
}

public class CoBoAccountGrain: Grain<CoBoAccountState>, ICoBoAccountGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    
    public CoBoAccountGrain(ICoBoProvider coBoProvider, 
        IObjectMapper objectMapper, 
        IOptionsSnapshot<CoBoOptions> coBoOptions)
    {
        _coBoProvider = coBoProvider;
        _objectMapper = objectMapper;
        _coBoOptions = coBoOptions;
    }

    public async Task<AssetDto> Get(string coin)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (!State.Assets.IsNullOrEmpty() && State.ExpireTime > now)
        {
            return State.Assets.FirstOrDefault(t => t.Coin == coin);
        }

        var accountResp = await _coBoProvider.GetAccountDetailAsync();
        if (accountResp == null || accountResp.Assets.IsNullOrEmpty()) return null;

        State = _objectMapper.Map<AccountDetailDto, CoBoAccountState>(accountResp);
        State.ExpireTime = now + _coBoOptions.Value.CoinExpireSeconds * 1000;
        State.LastModifyTime = now;
        await WriteStateAsync();

        return accountResp.Assets.FirstOrDefault(t => t.Coin == coin);
    }
}