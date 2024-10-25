using ETransferServer.Common;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.TokenLimitState;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Grains.Grain.TokenLimit;

public interface ITokenWithdrawLimitGrain : IGrainWithStringKey
{
    Task<bool> Acquire(decimal amount);
    Task Reverse(decimal amount);
    Task<TokenLimitGrainDto> GetLimit();

    public static string GenerateGrainId(string symbol)
    {
        return GuidHelper.GenerateGrainId(symbol, DateTime.UtcNow.Date.ToUtcString(TimeHelper.DatePattern));
    }
    
    public static string GenerateGrainId(string symbol, long timestamp)
    {
        return GuidHelper.GenerateGrainId(symbol, 
            TimeHelper.GetDateTimeFromTimeStamp(timestamp).Date.ToUtcString(TimeHelper.DatePattern));
    }
}

public class TokenWithdrawLimitGrain : Grain<TokenLimitState>, ITokenWithdrawLimitGrain
{
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly ILogger<TokenWithdrawLimitGrain> _logger;

    public TokenWithdrawLimitGrain(IOptionsSnapshot<WithdrawOptions> withdrawOptions, ILogger<TokenWithdrawLimitGrain> logger)
    {
        _withdrawOptions = withdrawOptions;
        _logger = logger;
    }

    public async Task<bool> Acquire(decimal amount)
    {
        if (!_withdrawOptions.Value.IsOpen || amount <= 0) return true;
        if (State.HasInit && State.RemainingLimit < amount) return false;
        State.RemainingLimit = !State.HasInit
            ? _withdrawOptions.Value.WithdrawThreshold - amount
            : State.RemainingLimit - amount;
        State.HasInit = true;

        _logger.LogInformation("minus remaining limit, minus:{minis}, remaining:{remaining}", amount,
            State.RemainingLimit);
        await WriteStateAsync();
        return true;
    }

    public async Task Reverse(decimal amount)
    {
        if (!_withdrawOptions.Value.IsOpen || amount <= 0 || !State.HasInit) return;
        if (State.RemainingLimit + amount < _withdrawOptions.Value.WithdrawThreshold)
        {
            State.RemainingLimit += amount;
        }

        _logger.LogInformation("reverse remaining limit, reverse:{reverse}, remaining:{remaining}", amount,
            State.RemainingLimit);
        await WriteStateAsync();
    }
    
    public async Task<TokenLimitGrainDto> GetLimit()
    {
        if (!_withdrawOptions.Value.IsOpen)
        {
            return new TokenLimitGrainDto { RemainingLimit = _withdrawOptions.Value.WithdrawThreshold };
        }

        if (!State.HasInit)
        {
            State.RemainingLimit = _withdrawOptions.Value.WithdrawThreshold;
            State.HasInit = true;
            await WriteStateAsync();
        }

        return new TokenLimitGrainDto { RemainingLimit = State.RemainingLimit};
    }
}