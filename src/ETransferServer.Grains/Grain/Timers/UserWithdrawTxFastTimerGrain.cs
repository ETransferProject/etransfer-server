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

public interface IUserWithdrawTxFastTimerGrain : IBaseTxFastTimerGrain
{
}

public class UserWithdrawTxFastTimerGrain : AbstractTxFastTimerGrain<WithdrawOrderDto>, IUserWithdrawTxFastTimerGrain
{
    private readonly ILogger<UserWithdrawTxFastTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IUserWithdrawProvider _userWithdrawProvider;

    public UserWithdrawTxFastTimerGrain(ILogger<UserWithdrawTxFastTimerGrain> logger,
        IContractProvider contractProvider, IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<TimerOptions> timerOptions, IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        ITokenTransferProvider transferProvider, IUserWithdrawProvider userWithdrawProvider) : base(logger,
        contractProvider, chainOptions, timerOptions, withdrawOptions, transferProvider, userWithdrawProvider)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _userWithdrawProvider = userWithdrawProvider;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("UserWithdrawTxFastTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync();

        _logger.LogDebug("UserWithdrawTxFastTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawFromFastTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.WithdrawFromFastTimer.PeriodSeconds));
    }
    
    internal override async Task SaveOrder(WithdrawOrderDto order)
    {
        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(order.Id);
        var res = await recordGrain.AddOrUpdate(order);
        await _userWithdrawProvider.AddOrUpdateSync(res.Value);
    }

    internal override async Task SaveOrder(WithdrawOrderDto order, Dictionary<string, string> externalInfo)
    {
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(order.Id);
        await userWithdrawGrain.AddOrUpdateOrder(order, externalInfo);
    }

    internal override async Task<WithdrawOrderDto> GetOrder(Guid orderId)
    {
        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var res = await recordGrain.Get();
        if (!res.Success)
        {
            _logger.LogWarning("Withdraw order {OrderId} not found", orderId);
            return null;
        }
        return res.Data as WithdrawOrderDto;
    }
}