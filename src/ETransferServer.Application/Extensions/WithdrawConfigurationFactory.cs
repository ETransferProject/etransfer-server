using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ETransferServer.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Factory for managing withdraw configuration operations
/// Provides centralized access to all withdraw-related configurations
/// </summary>
public class WithdrawConfigurationFactory
{
    private readonly IOptionsSnapshot<WithdrawConfigurationOptions> _withdrawOptions;

    public WithdrawConfigurationFactory(IOptionsSnapshot<WithdrawConfigurationOptions> withdrawOptions)
    {
        _withdrawOptions = withdrawOptions;
    }

    /// <summary>
    /// Validates if a withdrawal operation is allowed based on configuration
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <param name="amount">Withdrawal amount</param>
    /// <param name="userRegion">User's region/country</param>
    /// <param name="userAddress">User's withdrawal address</param>
    /// <returns>Validation result with details</returns>
    public WithdrawValidationResult ValidateWithdraw(string token, decimal amount, string userRegion = null, string userAddress = null)
    {
        var config = _withdrawOptions.Value;
        var result = new WithdrawValidationResult { IsValid = true };

        // Check if withdrawals are globally enabled
        if (!config.OrderConfig.IsEnabled)
        {
            result.IsValid = false;
            result.Message = "Withdrawal operations are currently disabled";
            return result;
        }

        // Check if risk management is enabled
        if (config.RiskConfig.IsEnabled)
        {
            // Check amount limits
            if (amount > config.RiskConfig.MaxWithdrawWithoutVerification)
            {
                result.RequiresVerification = true;
                result.VerificationReason = $"Amount {amount} exceeds limit without verification";
            }

            // Check blocked regions
            if (!string.IsNullOrEmpty(userRegion) && 
                config.RiskConfig.BlockedRegions.Contains(userRegion))
            {
                result.IsValid = false;
                result.Message = $"Withdrawals from region {userRegion} are not allowed";
                return result;
            }

            // Check suspicious address patterns
            if (!string.IsNullOrEmpty(userAddress))
            {
                foreach (var pattern in config.RiskConfig.SuspiciousAddressPatterns)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(userAddress, pattern))
                    {
                        result.IsValid = false;
                        result.Message = "Address matches suspicious pattern";
                        return result;
                    }
                }
            }
        }

        // Check minimum withdrawal amount
        if (amount < config.FeeConfig.MinWithdraw)
        {
            result.IsValid = false;
            result.Message = $"Minimum withdrawal amount is {config.FeeConfig.MinWithdraw}";
            return result;
        }

        return result;
    }

    /// <summary>
    /// Calculates the withdrawal fee for a transaction
    /// </summary>
    /// <param name="network">Network identifier</param>
    /// <param name="token">Token symbol</param>
    /// <param name="amount">Withdrawal amount</param>
    /// <returns>Calculated fee information</returns>
    public WithdrawFeeInfo CalculateWithdrawFee(string network, string token, decimal amount)
    {
        var config = _withdrawOptions.Value.FeeConfig;
        var feeInfo = new WithdrawFeeInfo();

        var networkTokenKey = $"{network}_{token}";

        // Get base fee from third-party configuration
        var minFee = config.MinThirdPartyFees.GetValueOrDefault(networkTokenKey, 0);
        var maxFee = config.MaxThirdPartyFees.GetValueOrDefault(networkTokenKey, decimal.MaxValue);

        feeInfo.BaseFee = minFee;
        feeInfo.MinFee = minFee;
        feeInfo.MaxFee = maxFee;

        // Apply dynamic fee calculation if enabled
        if (config.DynamicFeeConfig.IsEnabled)
        {
            feeInfo = CalculateDynamicFee(network, token, amount, config);
        }

        // Check for large amount thresholds
        if (config.LargeAmountThresholds.TryGetValue(token, out var threshold))
        {
            if (amount >= threshold)
            {
                feeInfo.IsLargeAmount = true;
                feeInfo.BaseFee *= 0.9m; // 10% discount for large amounts
            }
        }

        // Ensure fee is within bounds
        feeInfo.FinalFee = Math.Max(Math.Min(feeInfo.BaseFee, feeInfo.MaxFee), feeInfo.MinFee);
        feeInfo.NetworkTokenKey = networkTokenKey;

        return feeInfo;
    }

    /// <summary>
    /// Gets network information for a specific coin
    /// </summary>
    /// <param name="network">Network identifier</param>
    /// <param name="token">Token symbol</param>
    /// <returns>Network information or null if not found</returns>
    public WithdrawNetworkInfo GetNetworkInfo(string network, string token)
    {
        var coin = $"{network}_{token}";
        return _withdrawOptions.Value.NetworkConfig.NetworkInfos
            .FirstOrDefault(n => n.Coin.Equals(coin, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets transaction threshold configuration for a token
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <returns>Transaction threshold configuration</returns>
    public WithdrawTransactionThreshold GetTransactionThreshold(string token)
    {
        return _withdrawOptions.Value.NetworkConfig.TransactionThresholds.GetValueOrDefault(token);
    }

    /// <summary>
    /// Checks if an address is whitelisted for a network
    /// </summary>
    /// <param name="network">Network identifier</param>
    /// <param name="address">Address to check</param>
    /// <returns>True if address is whitelisted</returns>
    public bool IsAddressWhitelisted(string network, string address)
    {
        var whitelist = _withdrawOptions.Value.NetworkConfig.SupportWhiteLists.GetValueOrDefault(network);
        return whitelist?.Contains(address) == true;
    }

    /// <summary>
    /// Gets the payment address for a specific chain and token
    /// </summary>
    /// <param name="chainId">Chain identifier</param>
    /// <param name="token">Token symbol</param>
    /// <returns>Payment address or null if not found</returns>
    public string GetPaymentAddress(string chainId, string token)
    {
        var paymentAddresses = _withdrawOptions.Value.OrderConfig.PaymentAddresses;
        
        if (paymentAddresses.TryGetValue(chainId, out var tokenAddresses))
        {
            return tokenAddresses.GetValueOrDefault(token);
        }

        return null;
    }

    /// <summary>
    /// Validates velocity limits for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="amount">Withdrawal amount</param>
    /// <param name="recentWithdrawals">Recent withdrawal history</param>
    /// <returns>Velocity validation result</returns>
    public WithdrawVelocityResult ValidateVelocity(string userId, decimal amount, List<WithdrawHistoryItem> recentWithdrawals)
    {
        var velocityConfig = _withdrawOptions.Value.RiskConfig.VelocityConfig;
        var result = new WithdrawVelocityResult { IsValid = true };

        if (!velocityConfig.IsEnabled)
        {
            return result;
        }

        var timeWindow = DateTime.UtcNow.AddMinutes(-velocityConfig.TimeWindowMinutes);
        var recentWithdrawalsInWindow = recentWithdrawals
            .Where(w => w.Timestamp >= timeWindow)
            .ToList();

        // Check withdrawal count limit
        if (recentWithdrawalsInWindow.Count >= velocityConfig.MaxWithdrawalsPerHour)
        {
            result.IsValid = false;
            result.Message = $"Maximum {velocityConfig.MaxWithdrawalsPerHour} withdrawals per hour exceeded";
            return result;
        }

        // Check amount limit
        var totalAmount = recentWithdrawalsInWindow.Sum(w => w.Amount) + amount;
        if (totalAmount > velocityConfig.MaxAmountPerHour)
        {
            result.IsValid = false;
            result.Message = $"Maximum withdrawal amount per hour ({velocityConfig.MaxAmountPerHour}) exceeded";
            return result;
        }

        result.RemainingCount = velocityConfig.MaxWithdrawalsPerHour - recentWithdrawalsInWindow.Count;
        result.RemainingAmount = velocityConfig.MaxAmountPerHour - recentWithdrawalsInWindow.Sum(w => w.Amount);

        return result;
    }

    /// <summary>
    /// Checks if a withdrawal amount qualifies for notifications
    /// </summary>
    /// <param name="amount">Withdrawal amount</param>
    /// <returns>True if notification should be sent</returns>
    public bool ShouldNotifyLargeWithdrawal(decimal amount)
    {
        var config = _withdrawOptions.Value.NotificationConfig;
        return config.NotifyLargeWithdrawals && amount >= config.LargeWithdrawalThreshold;
    }

    /// <summary>
    /// Checks if fee fluctuation exceeds notification threshold
    /// </summary>
    /// <param name="currentFee">Current fee amount</param>
    /// <param name="previousFee">Previous fee amount</param>
    /// <returns>True if notification should be sent</returns>
    public bool ShouldNotifyFeeFluctuation(decimal currentFee, decimal previousFee)
    {
        var config = _withdrawOptions.Value.NotificationConfig;
        if (!config.NotifyFeeFluctuations || previousFee == 0)
        {
            return false;
        }

        var fluctuationPercent = Math.Abs(currentFee - previousFee) / previousFee;
        return fluctuationPercent >= config.FeeFluctuationThreshold;
    }

    /// <summary>
    /// Gets the order configuration
    /// </summary>
    /// <returns>Order configuration</returns>
    public WithdrawOrderConfig GetOrderConfig()
    {
        return _withdrawOptions.Value.OrderConfig;
    }

    /// <summary>
    /// Gets the risk configuration
    /// </summary>
    /// <returns>Risk configuration</returns>
    public WithdrawRiskConfig GetRiskConfig()
    {
        return _withdrawOptions.Value.RiskConfig;
    }

    /// <summary>
    /// Gets the performance configuration
    /// </summary>
    /// <returns>Performance configuration</returns>
    public WithdrawPerformanceConfig GetPerformanceConfig()
    {
        return _withdrawOptions.Value.PerformanceConfig;
    }

    /// <summary>
    /// Gets third-party service configuration
    /// </summary>
    /// <returns>Third-party configuration</returns>
    public WithdrawThirdPartyConfig GetThirdPartyConfig()
    {
        return _withdrawOptions.Value.ThirdPartyConfig;
    }

    /// <summary>
    /// Checks if risk score exceeds threshold
    /// </summary>
    /// <param name="riskScore">Risk score (0-100)</param>
    /// <returns>True if risk is too high</returns>
    public bool IsHighRisk(int riskScore)
    {
        return riskScore >= _withdrawOptions.Value.RiskConfig.RiskScoreThreshold;
    }

    /// <summary>
    /// Gets transfer path for cross-chain operations
    /// </summary>
    /// <param name="fromNetwork">Source network</param>
    /// <param name="toNetwork">Destination network</param>
    /// <returns>Transfer path or null if not found</returns>
    public List<string> GetTransferPath(string fromNetwork, string toNetwork)
    {
        var pathKey = $"{fromNetwork}_{toNetwork}";
        return _withdrawOptions.Value.NetworkConfig.TransferPaths.GetValueOrDefault(pathKey);
    }

    #region Private Methods

    private WithdrawFeeInfo CalculateDynamicFee(string network, string token, decimal amount, WithdrawFeeConfig config)
    {
        var dynamicConfig = config.DynamicFeeConfig;
        var networkTokenKey = $"{network}_{token}";
        var baseFee = config.MinThirdPartyFees.GetValueOrDefault(networkTokenKey, 0);

        var feeInfo = new WithdrawFeeInfo
        {
            BaseFee = baseFee,
            FeeType = "dynamic"
        };

        // Apply network congestion multiplier
        feeInfo.BaseFee *= dynamicConfig.NetworkCongestionMultiplier;

        // Apply volume-based multiplier
        feeInfo.BaseFee *= dynamicConfig.VolumeBasedMultiplier;

        // Apply time-based multiplier
        var currentHour = DateTime.UtcNow.Hour;
        if (dynamicConfig.TimeBasedMultipliers.TryGetValue(currentHour, out var timeMultiplier))
        {
            feeInfo.BaseFee *= timeMultiplier;
        }

        return feeInfo;
    }

    #endregion
}

/// <summary>
/// Result of withdrawal validation
/// </summary>
public class WithdrawValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; }
    public bool RequiresVerification { get; set; }
    public string VerificationReason { get; set; }
}

/// <summary>
/// Withdrawal fee calculation information
/// </summary>
public class WithdrawFeeInfo
{
    public decimal BaseFee { get; set; }
    public decimal FinalFee { get; set; }
    public decimal MinFee { get; set; }
    public decimal MaxFee { get; set; }
    public string FeeType { get; set; } = "fixed";
    public bool IsLargeAmount { get; set; }
    public string NetworkTokenKey { get; set; }
    public string Description { get; set; }
}

/// <summary>
/// Velocity check result for withdrawal limits
/// </summary>
public class WithdrawVelocityResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; }
    public int RemainingCount { get; set; }
    public decimal RemainingAmount { get; set; }
}

/// <summary>
/// Withdrawal history item for velocity checks
/// </summary>
public class WithdrawHistoryItem
{
    public string UserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string Network { get; set; }
    public string Token { get; set; }
} 