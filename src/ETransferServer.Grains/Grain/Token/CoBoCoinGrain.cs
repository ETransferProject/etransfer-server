using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ICoBoCoinGrain : IGrainWithStringKey
{

    public static string Id(string network, string symbol)
    {
        return string.Join(CommonConstant.Underline, network, symbol);
    }
    
    Task<CoBoCoinDto> Get();
    
    Task<CoBoCoinDto> GetCache();
}

public class CoBoCoinGrain: Grain<CoBoCoinState>, ICoBoCoinGrain
{

    private readonly IObjectMapper _objectMapper;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    
    public CoBoCoinGrain(ICoBoProvider coBoProvider, 
        IObjectMapper objectMapper, 
        IOptionsSnapshot<CoBoOptions> coBoOptions,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions)
    {
        _coBoProvider = coBoProvider;
        _objectMapper = objectMapper;
        _coBoOptions = coBoOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
    }

    public async Task<CoBoCoinDto> Get()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.Coin.NotNullOrEmpty() && State.ExpireTime > now)
        {
            return _objectMapper.Map<CoBoCoinState, CoBoCoinDto>(State);
        }

        var netWorkInfo = _withdrawNetworkOptions.Value.NetworkInfos.FirstOrDefault(t =>
            t.Coin.Equals(this.GetPrimaryKeyString(), StringComparison.OrdinalIgnoreCase));
        var coinResp = await _coBoProvider.GetCoinDetailAsync(this.GetPrimaryKeyString(), netWorkInfo?.Amount.ToString());
        if (coinResp == null) return null; 
        
        State = _objectMapper.Map<CoBoCoinDetailDto, CoBoCoinState>(coinResp);
        State.ExpireTime = now + _coBoOptions.Value.CoinExpireSeconds * 1000;
        State.LastModifyTime = now;
        await WriteStateAsync();

        return _objectMapper.Map<CoBoCoinState, CoBoCoinDto>(State);
    }
    
    public async Task<CoBoCoinDto> GetCache()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.Coin.NotNullOrEmpty() && State.ExpireTime > now)
        {
            return _objectMapper.Map<CoBoCoinState, CoBoCoinDto>(State);
        }

        return null; 
    }
}