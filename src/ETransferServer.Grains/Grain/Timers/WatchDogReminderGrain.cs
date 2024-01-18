using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
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
    private readonly IOptionsMonitor<TimerOptions> _timerOptions;


    public WatchDogReminderGrain(IReminderRegistry reminderRegistry, ILogger<WatchDogReminderGrain> logger,
        IOptionsMonitor<TimerOptions> timerOptions)
    {
        _reminderRegistry = reminderRegistry;
        _logger = logger;
        _timerOptions = timerOptions;
    }

    // reminder min period is 1 minute.
    public async Task StartReminder()
    {
        _logger.LogDebug("Startup dueTimeSec={Due}, periodSec={Per}",
            _timerOptions.CurrentValue.WatchDogReminder.DelaySeconds,
            _timerOptions.CurrentValue.WatchDogReminder.PeriodSeconds);
        _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
            reminderName: nameof(WatchDogReminderGrain),
            dueTime: TimeSpan.FromSeconds(_timerOptions.CurrentValue.WatchDogReminder.DelaySeconds),
            period: TimeSpan.FromSeconds(_timerOptions.CurrentValue.WatchDogReminder.PeriodSeconds));
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

        var userWithdrawTxTimerGrain = GrainFactory.GetGrain<IUserWithdrawTxTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserWithdrawTxTimerGrain)));
        userWithdrawTxTimerGrain.GetLastCallBackTime();

        var tokenAddressTimerGrain = GrainFactory.GetGrain<ITokenAddressTimerGrain>(
                GuidHelper.UniqGuid(nameof(ITokenAddressTimerGrain)));
        tokenAddressTimerGrain.GetLastCallBackTime();

        var withdrawTimerGrain = GrainFactory.GetGrain<IWithdrawTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
        withdrawTimerGrain.GetLastCallBackTime();
        
        var depositOrderRetryGrain =
            GrainFactory.GetGrain<IDepositOrderRetryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderRetryTimerGrain)));
        depositOrderRetryGrain.GetLastCallBackTime();
        
        return Task.CompletedTask;
    }
}