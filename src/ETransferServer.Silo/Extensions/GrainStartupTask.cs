using log4net.Core;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using ETransferServer.Common;
using ETransferServer.Grains.Grain.Timers;

namespace ETransferServer.Silo.Extensions;

public class GrainStartupTask : IStartupTask
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<GrainStartupTask> _logger;

    public GrainStartupTask(IGrainFactory grainFactory, ILogger<GrainStartupTask> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task Execute(CancellationToken cancellationToken)
    {
        _logger.LogDebug("GrainStartupTask start");

        var timerWatchDogReminder =
            _grainFactory.GetGrain<IWatchDogReminderGrain>(GuidHelper.UniqGuid(nameof(IWatchDogReminderGrain)));

        // After starting the reminder, you need to wait for a start interval before executing
        await timerWatchDogReminder.StartReminder();

        // Start all timers immediately
        await timerWatchDogReminder.StartupTimer();
    }
}