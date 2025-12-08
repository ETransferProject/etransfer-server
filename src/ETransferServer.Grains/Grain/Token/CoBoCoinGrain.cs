using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
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
    Task<int> GetConfirmingThreshold();
    Task<int> GetHomogeneousConfirmingThreshold(decimal amount);
}

public class CoBoCoinGrain: Grain<CoBoCoinState>, ICoBoCoinGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    
    public CoBoCoinGrain(ICoBoProvider coBoProvider, 
        IObjectMapper objectMapper, 
        IOptionsSnapshot<CoBoOptions> coBoOptions,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        IOptionsSnapshot<ChainOptions> chainOptions)
    {
        _coBoProvider = coBoProvider;
        _objectMapper = objectMapper;
        _coBoOptions = coBoOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _withdrawOptions = withdrawOptions;
        _chainOptions = chainOptions;
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
    
    public async Task<int> GetConfirmingThreshold()
    {
        var netWorkInfo = _withdrawNetworkOptions.Value.NetworkInfos.FirstOrDefault(t =>
            t.Coin.Equals(this.GetPrimaryKeyString(), StringComparison.OrdinalIgnoreCase));
        return netWorkInfo?.ConfirmNum ?? 0;
    }
    
    public async Task<int> GetHomogeneousConfirmingThreshold(decimal amount)
    {
        var split = this.GetPrimaryKeyString().Split(CommonConstant.Underline);
        if (split.Length < 2)
        {
            return 0;
        }
        if(split[0] == ChainId.AELF) return _chainOptions.Value.Contract.SafeBlockHeight;
        _withdrawOptions.Value.Homogeneous.TryGetValue(split[1], out var threshold);
        var amountThreshold = threshold?.AmountThreshold ?? 0L;
        var blockHeightUpperThreshold = threshold?.BlockHeightUpperThreshold ?? 0L;
        var blockHeightLowerThreshold = threshold?.BlockHeightLowerThreshold ?? 0L;
        return amount <= int.Parse(amountThreshold.ToString())
            ? int.Parse(blockHeightLowerThreshold.ToString())
            : int.Parse(blockHeightUpperThreshold.ToString());
    }
}