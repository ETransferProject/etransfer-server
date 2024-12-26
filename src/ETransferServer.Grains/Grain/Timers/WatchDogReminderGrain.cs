using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Runtime;
using Orleans.Timers;
using ETransferServer.Common;
using ETransferServer.Grains.Options;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWatchDogReminderGrain : IGrainWithGuidKey
{
    Task StartReminder();

    Task StartupTimer();
}

public class WatchDogReminderGrain : Orleans.Grain, IWatchDogReminderGrain, IRemindable
{
    private readonly ILogger<WatchDogReminderGrain> _logger;
    private readonly IReminderRegistry _reminderRegistry;
    private IGrainReminder _reminder;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;


    public WatchDogReminderGrain(IReminderRegistry reminderRegistry, ILogger<WatchDogReminderGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions)
    {
        _reminderRegistry = reminderRegistry;
        _logger = logger;
        _timerOptions = timerOptions;
    }

    // reminder min period is 1 minute.
    public async Task StartReminder()
    {
        _logger.LogDebug("Startup dueTimeSec={Due}, periodSec={Per}",
            _timerOptions.Value.WatchDogReminder.DelaySeconds,
            _timerOptions.Value.WatchDogReminder.PeriodSeconds);
        _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
            this.GetGrainId(),
            reminderName: nameof(WatchDogReminderGrain),
            dueTime: TimeSpan.FromSeconds(_timerOptions.Value.WatchDogReminder.DelaySeconds),
            period: TimeSpan.FromSeconds(_timerOptions.Value.WatchDogReminder.PeriodSeconds));
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        return StartupTimer();
    }

    public Task StartupTimer()
    {
        _logger.LogDebug("Active timers..");

        var coBoDepositQueryTimerGrain = GrainFactory.GetGrain<ICoBoDepositQueryTimerGrain>(
                GuidHelper.UniqGuid(nameof(ICoBoDepositQueryTimerGrain)));
        coBoDepositQueryTimerGrain.GetLastCallbackTime();

        var userDepositTxTimerGrain = GrainFactory.GetGrain<IUserDepositTxTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserDepositTxTimerGrain)));
        userDepositTxTimerGrain.GetLastCallBackTime();
        
        var swapTxTimerGrain = GrainFactory.GetGrain<ISwapTxTimerGrain>(
            GuidHelper.UniqGuid(nameof(ISwapTxTimerGrain)));
        swapTxTimerGrain.GetLastCallBackTime();

        var userWithdrawTxTimerGrain = GrainFactory.GetGrain<IUserWithdrawTxTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserWithdrawTxTimerGrain)));
        userWithdrawTxTimerGrain.GetLastCallBackTime();
        
        var userWithdrawTxFastTimerGrain = GrainFactory.GetGrain<IUserWithdrawTxFastTimerGrain>(
            GuidHelper.UniqGuid(nameof(IUserWithdrawTxFastTimerGrain)));
        userWithdrawTxFastTimerGrain.GetLastCallBackTime();

        var tokenAddressTimerGrain = GrainFactory.GetGrain<ITokenAddressTimerGrain>(
                GuidHelper.UniqGuid(nameof(ITokenAddressTimerGrain)));
        tokenAddressTimerGrain.GetLastCallBackTime();
        
        var tokenAddressRecycleTimerGrain = GrainFactory.GetGrain<ITokenAddressRecycleTimerGrain>(
            GuidHelper.UniqGuid(nameof(ITokenAddressRecycleTimerGrain)));
        tokenAddressRecycleTimerGrain.GetLastCallBackTime();
        
        var tokenIntegrateTimerGrain = GrainFactory.GetGrain<ITokenIntegrateTimerGrain>(
            GuidHelper.UniqGuid(nameof(ITokenIntegrateTimerGrain)));
        tokenIntegrateTimerGrain.GetLastCallBackTime();
        
        var tokenLiquidityTimerGrain = GrainFactory.GetGrain<ITokenLiquidityTimerGrain>(
            GuidHelper.UniqGuid(nameof(ITokenLiquidityTimerGrain)));
        tokenLiquidityTimerGrain.GetLastCallBackTime();

        var withdrawCoboTimerGrain = GrainFactory.GetGrain<IWithdrawCoboTimerGrain>(
            GuidHelper.UniqGuid(nameof(IWithdrawCoboTimerGrain)));
        withdrawCoboTimerGrain.GetLastCallBackTime();
        
        var withdrawTimerGrain = GrainFactory.GetGrain<IWithdrawTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
        withdrawTimerGrain.GetLastCallBackTime();
        
        var withdrawQueryTimerGrain = GrainFactory.GetGrain<IWithdrawQueryTimerGrain>(
            GuidHelper.UniqGuid(nameof(IWithdrawQueryTimerGrain)));
        withdrawQueryTimerGrain.GetLastCallbackTime();
        
        var depositOrderRetryGrain =
            GrainFactory.GetGrain<IDepositOrderRetryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderRetryTimerGrain)));
        depositOrderRetryGrain.GetLastCallBackTime();
        
        var withdrawOrderRetryGrain =
            GrainFactory.GetGrain<IWithdrawOrderRetryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawOrderRetryTimerGrain)));
        withdrawOrderRetryGrain.GetLastCallBackTime();
        
        return Task.CompletedTask;
    }
}