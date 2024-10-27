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

        var dto = new TokenPoolDto();
        foreach (var item in result.Assets)
        {
            if (!_withdrawNetworkOptions.Value.NetworkInfos.Exists(i => i.Coin == item.Coin)) continue;
            var split = item.Coin.Split(CommonConstant.Underline);
            if (split.Length < 2) continue;
            if (!dto.MultiPool.ContainsKey(split[1])) dto.MultiPool[split[1]] = item.AbsBalance;
            else
                dto.MultiPool[split[1]] =
                    (dto.MultiPool[split[1]].SafeToDecimal() + item.AbsBalance.SafeToDecimal()).ToString();
            dto.MultiPool.AddOrReplace(item.Coin, item.AbsBalance);
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

        var feeInfo = await _tokenPoolProvider.GetFeeListAsync(true);
        dto.ThirdFeeInfo = feeInfo.Item1;
        dto.AelfFeeInfo = feeInfo.Item2;

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
        foreach (var kv in dto.ThirdFeeInfo)
        {
            changeDto.ThirdFeeInfo.AddOrReplace(kv.Key, yesDto != null && yesDto.ThirdFeeInfo.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.ThirdFeeInfo[kv.Key].SafeToDecimal()).ToString()
                : kv.Value);
        }
        foreach (var kv in dto.AelfFeeInfo)
        {
            changeDto.AelfFeeInfo.AddOrReplace(kv.Key, yesDto != null && yesDto.AelfFeeInfo.ContainsKey(kv.Key)
                ? (kv.Value.SafeToDecimal() - yesDto.AelfFeeInfo[kv.Key].SafeToDecimal()).ToString()
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