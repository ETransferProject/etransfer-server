using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWithdrawOrderRetryTimerGrain: IGrainWithGuidKey
{
    Task<DateTime> GetLastCallBackTime();
    Task AddToPendingList(Guid orderId, string retryFromStatus);
}

public class WithdrawOrderRetryTimerGrain : AbstractOrderRetryTimerGrain<WithdrawOrderDto, WithdrawOrderRetryState>, IWithdrawOrderRetryTimerGrain
{
    private readonly ILogger<WithdrawOrderRetryTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    public WithdrawOrderRetryTimerGrain(ILogger<WithdrawOrderRetryTimerGrain> logger, IOptionsSnapshot<TimerOptions> timerOptions) : base(logger)
    {
        _logger = logger;
        _timerOptions = timerOptions;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("WithdrawOrderRetryTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync(cancellationToken);

        RegisterTimer(TimerCallBack, State,
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawRetryTimer.DelaySeconds), 
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawRetryTimer.PeriodSeconds));
    }

    protected override async Task SaveOrder(WithdrawOrderDto order, Dictionary<string, string> externalInfo)
    {
        var withdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(order.Id);
        await withdrawRecordGrain.AddOrUpdateOrder(order);
    }

    protected override async Task<WithdrawOrderDto> GetOrder(Guid orderId)
    {
        var withdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var res = await withdrawRecordGrain.Get();
        if (!res.Success)
        {
            _logger.LogError("Get pending retry order {OrderId} error, message={Msg}", res.Message);
            return null;
        }
        return res.Data as WithdrawOrderDto;
         
    }
}