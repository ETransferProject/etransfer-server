using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Options;

namespace ETransferServer.Grains.Grain.Timers;

public interface IUserDepositTxTimerGrain : IBaseTxTimerGrain
{
}

public class UserDepositTxTimerGrain : AbstractTxTimerGrain<DepositOrderDto>, IUserDepositTxTimerGrain
{
    private readonly ILogger<UserDepositTxTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    public UserDepositTxTimerGrain(ILogger<UserDepositTxTimerGrain> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, IOptionsSnapshot<TimerOptions> timerOptions,
        ITokenTransferProvider transferProvider) : base(logger, contractProvider, chainOptions, timerOptions, transferProvider)
    {
        _logger = logger;
        _timerOptions = timerOptions;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("UserDepositTxTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync();

        _logger.LogDebug("UserDepositTxTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.PeriodSeconds)
        );
    }

    internal override async Task SaveOrder(DepositOrderDto order, Dictionary<string, string> externalInfo)
    {
        var userDepositGrain = GrainFactory.GetGrain<IUserDepositGrain>(order.Id);
        await userDepositGrain.AddOrUpdateOrder(order, externalInfo);
    }

    internal override async Task<DepositOrderDto> GetOrder(Guid orderId)
    {
        var userDepositRecord = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
        var resp = await userDepositRecord.GetAsync();
        if (!resp.Success)
        {
            _logger.LogWarning("Deposit order {OrderId} not found", orderId);
            return null;
        }
        return resp.Data as DepositOrderDto;
    }
}