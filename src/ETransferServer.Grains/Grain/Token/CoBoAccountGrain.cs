using ETransferServer.Common;
using ETransferServer.Grains.State.Token;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ICoBoAccountGrain : IGrainWithStringKey
{
    public static string Id(string network, string symbol)
    {
        return string.Join(CommonConstant.Underline, network, symbol);
    }
    
    Task<AssetDto> Get();
    Task AddOrUpdate(AssetDto dto);
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

    public async Task<AssetDto> Get()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.Coin.NotNullOrEmpty() && State.ExpireTime > now)
        {
            return _objectMapper.Map<CoBoAccountState, AssetDto>(State);
        }

        var accountResp = await _coBoProvider.GetAccountDetailAsync();
        if (accountResp == null || accountResp.Assets.IsNullOrEmpty()) return null;

        var result = new AssetDto();
        foreach (var item in accountResp.Assets)
        {
            if (item.Coin == this.GetPrimaryKeyString()) result = item;
            var accountGrain = GrainFactory.GetGrain<ICoBoAccountGrain>(item.Coin);
            await accountGrain.AddOrUpdate(item);
        }

        return result;
    }

    public async Task AddOrUpdate(AssetDto dto)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        State = _objectMapper.Map<AssetDto, CoBoAccountState>(dto);
        State.ExpireTime = now + _coBoOptions.Value.CoinExpireSeconds * 1000;
        State.LastModifyTime = now;
        await WriteStateAsync();
    }
}