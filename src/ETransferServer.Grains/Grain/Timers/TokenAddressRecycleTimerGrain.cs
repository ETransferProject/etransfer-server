using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Users;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITokenAddressRecycleTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallBackTime();
}

public class TokenAddressRecycleTimerGrain : Grain<TokenAddressRecycleState>, ITokenAddressRecycleTimerGrain
{
    private DateTime _lastCallBackTime;

    private readonly IUserAddressProvider _userAddressProvider;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOptions;
    private readonly ILogger<TokenAddressRecycleTimerGrain> _logger;
    
    public TokenAddressRecycleTimerGrain(IUserAddressProvider userAddressProvider,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOptions,
        ILogger<TokenAddressRecycleTimerGrain> logger)
    {
        _userAddressProvider = userAddressProvider;
        _timerOptions = timerOptions;
        _depositAddressOptions = depositAddressOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("TokenAddressRecycleTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);
        
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.TokenAddressRecycleTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.TokenAddressRecycleTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("TokenAddressRecycleTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _logger.LogDebug("TokenAddressRecycleTimerGrain callback");
        _lastCallBackTime = DateTime.UtcNow;

        var expiredList = await _userAddressProvider.GetExpiredAddressListAsync(_depositAddressOptions.Value.AssignedAddressExpiredHour);
        var addressLimitGrain = GrainFactory.GetGrain<ITokenAddressLimitGrain>(
            GuidHelper.UniqGuid(nameof(ITokenAddressLimitGrain)));
        _logger.LogDebug("ExpiredList count: {count}, current: {current}", expiredList.Count,
            await addressLimitGrain.GetCurrent());
        if (expiredList.Count == 0) return;

        var addressList = new List<UserAddressDto>();
        foreach (var item in expiredList)
        {
            var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(item.UserToken.Address);
            item.IsAssigned = false;
            item.OrderId = string.Empty;
            item.UpdateTime = DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow);
            addressList.Add(item);
            await addressGrain.AddOrUpdate(item);
        }

        await addressLimitGrain.Reverse(expiredList.Count);
        await _userAddressProvider.BulkAddSync(addressList);
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(_lastCallBackTime);
    }
}