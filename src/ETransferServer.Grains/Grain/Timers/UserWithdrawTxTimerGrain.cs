using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.GraphQL;
using ETransferServer.Grains.Options;
using ETransferServer.Options;

namespace ETransferServer.Grains.Grain.Timers;

public interface IUserWithdrawTxTimerGrain : IBaseTxTimerGrain
{
}

public class UserWithdrawTxTimerGrain : AbstractTxTimerGrain<WithdrawOrderDto>, IUserWithdrawTxTimerGrain
{
    private readonly ILogger<UserWithdrawTxTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;

    public UserWithdrawTxTimerGrain(ILogger<UserWithdrawTxTimerGrain> logger,
        IContractProvider contractProvider, IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<TimerOptions> timerOptions, ITokenTransferProvider transferProvider) : base(logger,
        contractProvider, chainOptions, timerOptions, transferProvider)
    {
        _logger = logger;
        _timerOptions = timerOptions;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("UserWithdrawTxTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync();

        _logger.LogDebug("UserWithdrawTxTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawFromTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawFromTimer.PeriodSeconds));
    }

    internal override async Task SaveOrder(WithdrawOrderDto order, Dictionary<string, string> externalInfo)
    {
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(order.Id);
        await userWithdrawGrain.AddOrUpdateOrder(order, externalInfo);
    }

    internal override async Task<WithdrawOrderDto> GetOrder(Guid orderId)
    {
        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var res = await recordGrain.GetAsync();
        if (!res.Success)
        {
            _logger.LogWarning("Withdraw order {OrderId} not found", orderId);
            return null;
        }
        return res.Data as WithdrawOrderDto;
    }
}