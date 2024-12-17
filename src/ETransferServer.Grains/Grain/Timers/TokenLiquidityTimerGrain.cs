using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.TokenAccess;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenLiquidityTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenLiquidityTimerGrain : Grain<TokenLiquidityState>, ITokenLiquidityTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly ITokenAccessAppService _tokenAccessAppService;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    private const int PageSize = 1000;
    
    public TokenLiquidityTimerGrain(ITokenAccessAppService tokenAccessAppService, 
        ICoBoProvider coBoProvider,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _tokenAccessAppService = tokenAccessAppService;
        _coBoProvider = coBoProvider;
        _timerOptions = timerOptions;
        _tokenAccessOptions = tokenAccessOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenLiquidityTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenLiquidityTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenLiquidityTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;
        var skipCount = 0;
        var tokenApplyList = new List<TokenApplyDto>();

        while (true)
        {
            var resultDto = new PagedResultDto<TokenApplyOrderResultDto>();
            try
            {
                resultDto = await _tokenAccessAppService.GetTokenApplyListAsync(
                    new GetTokenApplyOrderListInput
                    {
                        Status = TokenApplyOrderStatus.Complete.ToString(),
                        SkipCount = skipCount,
                        MaxResultCount = PageSize
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get complete token apply order list error.");
            }
        
            if (resultDto == null || resultDto.Items == null || resultDto.Items.Count == 0)
            {
                break;
            }
        
            _logger.LogDebug("TokenLiquidityTimerGrain call, {skipCount}", skipCount);
            skipCount += resultDto.Items.Count;
            foreach (var item in resultDto.Items)
            {
                var chainId = item.OtherChainTokenInfo?.ChainId ?? (!item.ChainTokenInfo.IsNullOrEmpty()
                    ? item.ChainTokenInfo[0].ChainId
                    : null);
                if (chainId.IsNullOrEmpty() || tokenApplyList.Any(t => t.Symbol == item.Symbol && 
                        t.Address == item.UserAddress && t.ChainId == chainId)) continue;
                
                tokenApplyList.Add(new TokenApplyDto
                {
                    Symbol = item.Symbol,
                    Address = item.UserAddress,
                    ChainId = chainId,
                    Coin = string.Join(CommonConstant.Underline, chainId, item.Symbol)
                });
            }
            
            if (resultDto.Items.Count < PageSize) break;
        }

        if (tokenApplyList.IsNullOrEmpty()) return;
        _logger.LogInformation("TokenLiquidityTimerGrain query count: {count}", tokenApplyList.Count);
        var result = await _coBoProvider.GetAccountDetailAsync();
        if (result == null || result.Assets.IsNullOrEmpty()) return;
        foreach (var item in result.Assets)
        {
            if (!tokenApplyList.Exists(t => t.Coin == item.Coin)) continue;
            
            var coin = item.Coin.Split(CommonConstant.Underline);
            var symbol = coin.Length == 1 ? coin[0] : coin[1];
            var exchangeSymbolPair = string.Join(CommonConstant.Underline, symbol, CommonConstant.Symbol.USDT);
            var avgExchange = 0M;
            try
            {
                var exchangeGrain = GrainFactory.GetGrain<ITokenExchangeGrain>(exchangeSymbolPair);
                var exchange = await exchangeGrain.GetAsync();
                AssertHelper.NotEmpty(exchange, "Exchange data not found {}", exchangeSymbolPair);
                avgExchange = exchange.Values
                    .Where(ex => ex.Exchange > 0)
                    .Average(ex => ex.Exchange);
                AssertHelper.IsTrue(avgExchange > 0, "Exchange amount error {}" + avgExchange);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exchange fail: {coin}", item.Coin);
            }
            if (avgExchange <= 0) continue;
            var amount = avgExchange * item.AbsBalance.SafeToDecimal();
            if (amount <= (!_tokenAccessOptions.Value.PoolConfig.ContainsKey(item.Coin)
                    ? _tokenAccessOptions.Value.DefaultPoolConfig.Liquidity.SafeToDecimal()
                    : _tokenAccessOptions.Value.PoolConfig[item.Coin].Liquidity.SafeToDecimal()))
            {
                var monitorGrain = GrainFactory.GetGrain<IUserTokenAccessMonitorGrain>(item.Coin);
                var tokenApply = tokenApplyList.FirstOrDefault(t => t.Coin == item.Coin);
                tokenApply.Amount = amount.ToString();
                await monitorGrain.DoLiquidityMonitor(tokenApply);
            }
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}