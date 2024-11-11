using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IWithdrawOrderCallGrain : IGrainWithGuidKey
{
    Task<int> AddOrGet(int status = 0);
    Task<bool> AddRetry();
}

public class WithdrawOrderCallGrain : Grain<WithdrawOrderCallState>, IWithdrawOrderCallGrain
{
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly ILogger<WithdrawOrderCallGrain> _logger;

    public WithdrawOrderCallGrain(IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        ILogger<WithdrawOrderCallGrain> logger)
    {
        _withdrawOptions = withdrawOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<int> AddOrGet(int status = 0)
    {
        _logger.LogInformation("WithdrawOrderCallGrain, orderId:{orderId}, status:{status}", 
            this.GetPrimaryKey(), status);
        if (status == 0) return State.Status;
        State.OrderId = this.GetPrimaryKey();
        State.Status = status;
        await WriteStateAsync();
        return status;
    }

    public async Task<bool> AddRetry()
    {
        State.OrderId = this.GetPrimaryKey();
        if (State.Status >= 1 && State.Status <= 2)
        {
            State.CallRetry += 1;
            _logger.LogInformation(
                "WithdrawOrderCallGrain, orderId:{orderId}, callRetry:{callRetry}, callMaxRetry:{callMaxRetry}",
                this.GetPrimaryKey(), State.CallRetry, _withdrawOptions.Value.CallMaxRetry);
            await WriteStateAsync();
            if (State.CallRetry > _withdrawOptions.Value.CallMaxRetry)
                return false;
        }
        else if(State.Status > 2)
        {
            State.CallbackRetry += 1;
            _logger.LogInformation(
                "WithdrawOrderCallGrain, orderId:{orderId}, callbackRetry:{callbackRetry}, callbackMaxRetry:{callbackMaxRetry}",
                this.GetPrimaryKey(), State.CallbackRetry, _withdrawOptions.Value.CallbackMaxRetry);
            await WriteStateAsync();
            if (State.CallbackRetry > _withdrawOptions.Value.CallbackMaxRetry)
                return false;
        }

        return true;
    }
}