using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Token;
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
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<TokenAccessOptions> _tokenAccessOptions;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    private const int PageSize = 1000;
    
    public TokenLiquidityTimerGrain(ITokenAccessAppService tokenAccessAppService, 
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<TokenAccessOptions> tokenAccessOptions,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _tokenAccessAppService = tokenAccessAppService;
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
        var symbolList = new List<string>();

        // while (true)
        // {
        //     var resultDto = new PagedResultDto<TokenApplyOrderResultDto>();
        //     try
        //     {
        //         resultDto = await _tokenAccessAppService.GetTokenApplyOrderPagedListAsync(
        //             new GetTokenApplyOrderListInput
        //             {
        //                 SkipCount = skipCount,
        //                 MaxResultCount = PageSize
        //             });
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError(e, "get token apply order list error.");
        //     }
        //
        //     if (resultDto == null || resultDto.Items == null || resultDto.Items.Count == 0)
        //     {
        //         break;
        //     }
        //
        //     _logger.LogDebug("TokenLiquidityTimerGrain call, {skipCount}", skipCount);
        //     skipCount += resultDto.Items.Count;
        //     foreach (var item in resultDto.Items)
        //     {
        //         if(symbolList.Contains(item.Symbol)) continue;
        //         
        //         var tokenInvokeGrain = GrainFactory.GetGrain<ITokenInvokeGrain>(item.Symbol);
        //         var liquidityInUsd = await tokenInvokeGrain.GetLiquidityInUsd(item.Symbol);
        //
        //         if (liquidityInUsd.SafeToDecimal() <= (!_tokenAccessOptions.Value.TokenConfig.ContainsKey(item.Symbol)
        //                 ? _tokenAccessOptions.Value.DefaultConfig.Liquidity.SafeToDecimal()
        //                 : _tokenAccessOptions.Value.TokenConfig[item.Symbol].Liquidity.SafeToDecimal()))
        //         {
        //             var monitorGrain = GrainFactory.GetGrain<IUserTokenAccessMonitorGrain>(item.Symbol);
        //             await monitorGrain.DoLiquidityMonitor(item.Symbol, item.UserAddress, liquidityInUsd);
        //         }
        //         symbolList.Add(item.Symbol);
        //     }
        //     
        //     if (resultDto.Items.Count < PageSize) break;
        // }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}