using AElf.Contracts.MultiToken;
using AElf.Types;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.AElfSdk.Dtos;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider;
using ETransferServer.Grains.State.Order;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenPoolTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallbackTime();
}

public class TokenPoolTimerGrain : Grain<TokenPoolTimerState>, ITokenPoolTimerGrain
{
    private DateTime? _lastCallbackTime;
    
    private readonly ILogger<TokenPoolTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOption;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IContractProvider _contractProvider;
    private readonly ITokenPoolProvider _tokenPoolProvider;
    
    public TokenPoolTimerGrain(ILogger<TokenPoolTimerGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions, 
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOption,
        ICoBoProvider coBoProvider,
        IContractProvider contractProvider,
        ITokenPoolProvider tokenPoolProvider)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _withdrawOption = withdrawOption;
        _coBoProvider = coBoProvider;
        _contractProvider = contractProvider;
        _tokenPoolProvider = tokenPoolProvider;
    }
    
    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenPoolTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenPoolTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenPoolTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenPoolTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _lastCallbackTime = DateTime.UtcNow;
        _logger.LogInformation("TokenPoolTimerGrain callback, {Time}", DateTime.UtcNow.ToUtc8String());

        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var today = DateTime.UtcNow.Date.ToUtcMilliSeconds();
        if (today < State.LastQueryTime) return;

        var result = await _coBoProvider.GetAccountDetailAsync();
        if (result == null || result.Assets.IsNullOrEmpty()) return;

        var assets = result.Assets
            .GroupBy(c => c.Coin)
            .Select(g => g.Last())
            .ToList();
        var dto = new TokenPoolDto();
        var feeCoinList = new List<string>();
        foreach (var item in assets)
        {
            if (!_withdrawNetworkOptions.Value.NetworkInfos.Exists(i => i.Coin == item.Coin)) continue;
            var split = item.Coin.Split(CommonConstant.Underline);
            if (split.Length < 2) continue;
            if (!dto.MultiPool.ContainsKey(split[1])) dto.MultiPool[split[1]] = item.AbsBalance;
            else
                dto.MultiPool[split[1]] =
                    (dto.MultiPool[split[1]].SafeToDecimal() + item.AbsBalance.SafeToDecimal()).ToString();
            dto.MultiPool.AddOrReplace(item.Coin, item.AbsBalance);
            
            if(!feeCoinList.Contains(item.FeeCoin)) feeCoinList.Add(item.FeeCoin);
        }
        foreach (var item in assets)
        {
            if (!feeCoinList.Contains(item.Coin)) continue;
            dto.ThirdPoolFeeInfo.AddOrReplace(item.Coin, item.AbsBalance);
        }

        dto.MultiPool = dto.MultiPool.OrderBy(k =>
                _withdrawNetworkOptions.Value.NetworkInfos.Select(n => n.Coin).ToList().IndexOf(k.Key))
            .ToDictionary(k => k.Key, v => v.Value);

        foreach (var kv in _withdrawOption.Value.PaymentAddresses)
        {
            var chainId = kv.Key;
            foreach (var subKv in kv.Value)
            {
                var balance = await _contractProvider.CallTransactionAsync<GetBalanceOutput>(chainId,
                    SystemContractName.TokenContract,
                    "GetBalance",
                    new GetBalanceInput
                    {
                        Owner = Address.FromBase58(subKv.Value),
                        Symbol = subKv.Key
                    });
                var tokenGrain =
                    GrainFactory.GetGrain<ITokenGrain>(ITokenGrain.GenGrainId(subKv.Key, chainId));
                var token = await tokenGrain.GetToken();

                var decimalPow = (decimal)Math.Pow(10, token.Decimals);
                var balanceDecimal = balance.Balance / decimalPow;
                if (!dto.TokenPool.ContainsKey(subKv.Key)) dto.TokenPool[subKv.Key] = balanceDecimal.ToString();
                else dto.TokenPool[subKv.Key] = (dto.TokenPool[subKv.Key].SafeToDecimal() + balanceDecimal).ToString();
                dto.TokenPool.AddOrReplace(string.Join(CommonConstant.Underline, chainId, subKv.Key),
                    balanceDecimal.ToString());
            }
        }
        
        foreach (var kv in _withdrawOption.Value.PaymentAddresses)
        {
            if (kv.Key != ChainId.AELF) continue;
            foreach (var subKey in kv.Value.Keys)
            {
                var multiPoolAmount = dto.MultiPool.ContainsKey(subKey)
                    ? dto.MultiPool[subKey].SafeToDecimal()
                    : 0M;
                var tokenPoolAmount = dto.TokenPool.ContainsKey(subKey)
                    ? dto.TokenPool[subKey].SafeToDecimal()
                    : 0M;
                dto.Pool[subKey] = (multiPoolAmount + tokenPoolAmount).ToString();
            }
        }

        var feeInfo = await _tokenPoolProvider.GetFeeListAsync(true);
        dto.ThirdFeeInfo = feeInfo.Item1;
        dto.WithdrawFeeInfo = feeInfo.Item2;
        dto.DepositFeeInfo = feeInfo.Item3;

        var tokenPoolGrain = GrainFactory.GetGrain<ITokenPoolGrain>(ITokenPoolGrain.GenerateGrainId());
        await tokenPoolGrain.AddOrUpdate(dto);

        var yesTokenPoolGrain =
            GrainFactory.GetGrain<ITokenPoolGrain>(
                ITokenPoolGrain.GenerateGrainId(DateTime.UtcNow.AddDays(-1).Date.ToUtcMilliSeconds()));
        var yesDto = await yesTokenPoolGrain.Get();

        var changeDto = new TokenPoolDto
        {
            Date = ITokenPoolGrain.GenerateGrainId()
        };
        foreach (var kv in dto.MultiPool)
        {
            changeDto.MultiPool.AddOrReplace(kv.Key, yesDto != null && yesDto.MultiPool.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.MultiPool[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.TokenPool)
        {
            changeDto.TokenPool.AddOrReplace(kv.Key, yesDto != null && yesDto.TokenPool.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.TokenPool[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.Pool)
        {
            changeDto.Pool.AddOrReplace(kv.Key, yesDto != null && yesDto.Pool.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.Pool[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.ThirdPoolFeeInfo)
        {
            changeDto.ThirdPoolFeeInfo.AddOrReplace(kv.Key, yesDto != null && yesDto.ThirdPoolFeeInfo.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.ThirdPoolFeeInfo[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.WithdrawFeeInfo)
        {
            changeDto.WithdrawFeeInfo.AddOrReplace(kv.Key, yesDto != null && yesDto.WithdrawFeeInfo.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.WithdrawFeeInfo[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.DepositFeeInfo)
        {
            changeDto.DepositFeeInfo.AddOrReplace(kv.Key, yesDto != null && yesDto.DepositFeeInfo.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.DepositFeeInfo[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        await _tokenPoolProvider.AddOrUpdateSync(changeDto);

        State.LastQueryTime = now;
        await WriteStateAsync();
    }

    public Task<DateTime> GetLastCallbackTime()
    {
        return Task.FromResult(_lastCallbackTime ?? DateTime.MinValue);
    }
}