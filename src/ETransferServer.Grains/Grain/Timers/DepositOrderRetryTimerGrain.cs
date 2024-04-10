using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Options;

namespace ETransferServer.Grains.Grain.Timers;

public interface IDepositOrderRetryTimerGrain: IGrainWithGuidKey
{
    Task<DateTime> GetLastCallBackTime();
    Task AddToPendingList(Guid orderId, string retryFromStatus);
}


public class DepositOrderRetryTimerGrain : AbstractOrderRetryTimerGrain<DepositOrderDto>, IDepositOrderRetryTimerGrain
{
    private readonly ILogger<DepositOrderRetryTimerGrain> _logger;
    private readonly IOptionsMonitor<TimerOptions> _timerOptions;


    public DepositOrderRetryTimerGrain(ILogger<DepositOrderRetryTimerGrain> logger, IOptionsMonitor<TimerOptions> timerOptions) : base(logger)
    {
        _logger = logger;
        _timerOptions = timerOptions;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("DepositOrderRetryTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync();

        RegisterTimer(TimerCallBack, State,
            TimeSpan.FromSeconds(_timerOptions.CurrentValue.DepositRetryTimer.DelaySeconds), 
            TimeSpan.FromSeconds(_timerOptions.CurrentValue.DepositRetryTimer.PeriodSeconds));
    }

    protected override async Task SaveOrder(DepositOrderDto order, Dictionary<string, string> externalInfo)
    {
        var depositRecordGrain = GrainFactory.GetGrain<IUserDepositGrain>(order.Id);
        await depositRecordGrain.AddOrUpdateOrder(order);
    }

    protected override async Task<DepositOrderDto> GetOrder(Guid orderId)
    {
        var depositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
        var res = await depositRecordGrain.GetAsync();
        if (!res.Success)
        {
            _logger.LogError("Get pending retry order {OrderId} error, message={Msg}", res.Message);
            return null;
        }
        return res.Data as DepositOrderDto;
         
    }
}