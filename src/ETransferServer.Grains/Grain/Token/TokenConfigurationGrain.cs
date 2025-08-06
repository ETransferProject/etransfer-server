using ETransferServer.Common;
using ETransferServer.Grains.State.Token;
using Microsoft.Extensions.Logging;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenConfigurationGrain : IGrainWithStringKey
{
    Task<TokenConfigurationState> GetConfigurationAsync();
    Task SetDepositEnabledAsync(bool enabled);
    Task SetWithdrawEnabledAsync(bool enabled);
    Task SetTransferEnabledAsync(bool enabled);
    Task SetAllEnabledAsync(bool depositEnabled, bool withdrawEnabled, bool transferEnabled);
    
    public static string GenGrainId(string symbol, string chainId)
    {
        return TokenConfigurationGrainId.Of(symbol, chainId).ToGrainId();
    }
}

public class TokenConfigurationGrain : Grain<TokenConfigurationState>, ITokenConfigurationGrain
{
    private readonly ILogger<TokenConfigurationGrain> _logger;

    public TokenConfigurationGrain(ILogger<TokenConfigurationGrain> logger)
    {
        _logger = logger;
    }

    public async Task<TokenConfigurationState> GetConfigurationAsync()
    {
        if (State.ChainId.IsNullOrEmpty())
        {
            var grainId = TokenConfigurationGrainId.FromGrainId(this.GetPrimaryKeyString());
            if (grainId == null)
            {
                _logger.LogWarning("Invalid grain id: {GrainId}", this.GetPrimaryKeyString());
                return State;
            }
            
            State.ChainId = grainId.ChainId;
            State.Symbol = grainId.Symbol;
            State.LastUpdatedTime = DateTime.UtcNow;
            await WriteStateAsync();
        }
        
        return State;
    }

    public async Task SetDepositEnabledAsync(bool enabled)
    {
        await EnsureStateInitializedAsync();
        
        if (State.DepositEnabled != enabled)
        {
            State.DepositEnabled = enabled;
            State.LastUpdatedTime = DateTime.UtcNow;
            await WriteStateAsync();
            
            _logger.LogInformation("Updated deposit status for {Symbol} on {ChainId}: {Enabled}", 
                State.Symbol, State.ChainId, enabled);
        }
    }

    public async Task SetWithdrawEnabledAsync(bool enabled)
    {
        await EnsureStateInitializedAsync();
        
        if (State.WithdrawEnabled != enabled)
        {
            State.WithdrawEnabled = enabled;
            State.LastUpdatedTime = DateTime.UtcNow;
            await WriteStateAsync();
            
            _logger.LogInformation("Updated withdraw status for {Symbol} on {ChainId}: {Enabled}", 
                State.Symbol, State.ChainId, enabled);
        }
    }

    public async Task SetTransferEnabledAsync(bool enabled)
    {
        await EnsureStateInitializedAsync();
        
        if (State.TransferEnabled != enabled)
        {
            State.TransferEnabled = enabled;
            State.LastUpdatedTime = DateTime.UtcNow;
            await WriteStateAsync();
            
            _logger.LogInformation("Updated transfer status for {Symbol} on {ChainId}: {Enabled}", 
                State.Symbol, State.ChainId, enabled);
        }
    }

    public async Task SetAllEnabledAsync(bool depositEnabled, bool withdrawEnabled, bool transferEnabled)
    {
        await EnsureStateInitializedAsync();
        
        bool hasChanges = State.DepositEnabled != depositEnabled || 
                         State.WithdrawEnabled != withdrawEnabled || 
                         State.TransferEnabled != transferEnabled;
        
        if (hasChanges)
        {
            State.DepositEnabled = depositEnabled;
            State.WithdrawEnabled = withdrawEnabled;
            State.TransferEnabled = transferEnabled;
            State.LastUpdatedTime = DateTime.UtcNow;
            await WriteStateAsync();
            
            _logger.LogInformation("Updated all statuses for {Symbol} on {ChainId}: Deposit={DepositEnabled}, Withdraw={WithdrawEnabled}, Transfer={TransferEnabled}", 
                State.Symbol, State.ChainId, depositEnabled, withdrawEnabled, transferEnabled);
        }
    }

    private async Task EnsureStateInitializedAsync()
    {
        if (State.ChainId.IsNullOrEmpty())
        {
            await GetConfigurationAsync();
        }
    }
}

public class TokenConfigurationGrainId
{
    public string Symbol { get; set; }
    public string ChainId { get; set; }

    public TokenConfigurationGrainId(string symbol, string chainId)
    {
        Symbol = symbol;
        ChainId = chainId;
    }

    public string ToGrainId()
    {
        return string.Join(CommonConstant.Hyphen, ChainId, Symbol);
    }

    public static TokenConfigurationGrainId Of(string symbol, string chainId)
    {
        return new TokenConfigurationGrainId(symbol, chainId);
    }

    public static TokenConfigurationGrainId FromGrainId(string grainId)
    {
        var vals = grainId.Split(CommonConstant.Hyphen);
        if (vals.Length < 2 || vals[0].IsNullOrEmpty() || vals[1].IsNullOrEmpty())
        {
            return null;
        }

        return new TokenConfigurationGrainId(grainId.Substring(vals[0].Length + 1), vals[0]);
    }
}
