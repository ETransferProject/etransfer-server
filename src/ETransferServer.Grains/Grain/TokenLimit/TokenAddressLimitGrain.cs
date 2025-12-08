using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.TokenLimitState;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Grains.Grain.TokenLimit;

public interface ITokenAddressLimitGrain : IGrainWithGuidKey
{
    Task<bool> Acquire(int count = 1);
    Task Reverse(int count = 1);
    Task<int> GetCurrent();
}

public class TokenAddressLimitGrain : Grain<TokenAddressLimitState>, ITokenAddressLimitGrain
{
    private readonly IOptionsSnapshot<DepositAddressOptions> _addressOptions;
    private readonly ILogger<TokenAddressLimitGrain> _logger;

    public TokenAddressLimitGrain(IOptionsSnapshot<DepositAddressOptions> addressOptions, 
        ILogger<TokenAddressLimitGrain> logger)
    {
        _addressOptions = addressOptions;
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

    public async Task<bool> Acquire(int count = 1)
    {
        var currentCount = State.CurrentAssignedCount;
        currentCount += count;
        _logger.LogInformation("Address assigned acquire, current:{current}, max:{max}",
            currentCount, _addressOptions.Value.MaxAssignedTransferThreshold);
        if (currentCount > _addressOptions.Value.MaxAssignedTransferThreshold) return false;
        State.CurrentAssignedCount += count;
        await WriteStateAsync();
        return true;
    }

    public async Task Reverse(int count = 1)
    {
        State.CurrentAssignedCount -= count;
        
        _logger.LogInformation("Address assigned reverse, current:{current}, max:{max}",
            State.CurrentAssignedCount, _addressOptions.Value.MaxAssignedTransferThreshold);
        if (State.CurrentAssignedCount < 0) State.CurrentAssignedCount = 0;
        await WriteStateAsync();
    }
    
    public async Task<int> GetCurrent()
    {
        return State.CurrentAssignedCount;
    }
}