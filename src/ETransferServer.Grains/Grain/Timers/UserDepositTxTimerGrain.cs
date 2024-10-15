using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
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
    private readonly IUserDepositProvider _userDepositProvider;

    public UserDepositTxTimerGrain(ILogger<UserDepositTxTimerGrain> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, IOptionsSnapshot<TimerOptions> timerOptions,
        ITokenTransferProvider transferProvider, IUserWithdrawProvider userWithdrawProvider,
        IUserDepositProvider userDepositProvider) 
        : base(logger, contractProvider, chainOptions, timerOptions, transferProvider, userWithdrawProvider, userDepositProvider)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _userDepositProvider = userDepositProvider;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("UserDepositTxTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());
        await base.OnActivateAsync(cancellationToken);

        _logger.LogDebug("UserDepositTxTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, State,
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.DelaySeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.DepositTimer.PeriodSeconds)
        );
    }
    
    internal override async Task SaveOrder(DepositOrderDto order)
    {
        var recordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(order.Id);
        var res = await recordGrain.CreateOrUpdateAsync(order);
        await _userDepositProvider.AddOrUpdateSync(res.Value);
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