using ETransferServer.Common;
using ETransferServer.Dtos.Token;
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
    
    Task<CoBoCoinDto> GetAsync();
    
}

public class CoBoCoinGrain: Grain<CoBoCoinState>, ICoBoCoinGrain
{

    private readonly IObjectMapper _objectMapper;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsMonitor<CoBoOptions> _coBoOptions;
    

    public CoBoCoinGrain(ICoBoProvider coBoProvider, IObjectMapper objectMapper, IOptionsMonitor<CoBoOptions> coBoOptions)
    {
        _coBoProvider = coBoProvider;
        _objectMapper = objectMapper;
        _coBoOptions = coBoOptions;
    }

    public async Task<CoBoCoinDto> GetAsync()
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        if (State.Coin.NotNullOrEmpty() && State.ExpireTime > now)
        {
            return _objectMapper.Map<CoBoCoinState, CoBoCoinDto>(State);
        }

        var coinResp = await _coBoProvider.GetCoinDetailAsync(this.GetPrimaryKeyString());
        if (coinResp == null) return null; 
        
        State = _objectMapper.Map<CoBoCoinDetailDto, CoBoCoinState>(coinResp);
        State.ExpireTime = now + _coBoOptions.CurrentValue.CoinExpireSeconds * 1000;
        State.LastModifyTime = now;
        await WriteStateAsync();

        return _objectMapper.Map<CoBoCoinState, CoBoCoinDto>(State);
    }
    
    
    
}