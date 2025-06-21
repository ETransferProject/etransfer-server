using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using ETransferServer.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Factory for managing deposit configuration operations
/// Provides centralized access to all deposit-related configurations
/// </summary>
public class DepositConfigurationFactory
{
    private readonly IOptionsSnapshot<DepositConfigurationOptions> _depositOptions;

    public DepositConfigurationFactory(IOptionsSnapshot<DepositConfigurationOptions> depositOptions)
    {
        _depositOptions = depositOptions;
    }

    /// <summary>
    /// Validates if a deposit operation is allowed based on configuration
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <param name="amount">Deposit amount</param>
    /// <param name="userRegion">User's region/country</param>
    /// <returns>Validation result with details</returns>
    public DepositValidationResult ValidateDeposit(string token, decimal amount, string userRegion = null)
    {
        var config = _depositOptions.Value;
        var result = new DepositValidationResult { IsValid = true };

        // Check if risk management is enabled
        if (config.RiskConfig.IsEnabled)
        {
            // Check amount limits
            if (amount > config.RiskConfig.MaxDepositWithoutVerification)
            {
                result.RequiresVerification = true;
                result.VerificationReason = $"Amount {amount} exceeds limit without verification";
            }

            // Check blocked regions
            if (!string.IsNullOrEmpty(userRegion) && 
                config.RiskConfig.BlockedRegions.Contains(userRegion))
            {
                result.IsValid = false;
                result.Message = $"Deposits from region {userRegion} are not allowed";
                return result;
            }
        }

        // Check minimum deposit amount
        if (config.ServiceFeeConfig.MinDepositAmounts.TryGetValue(token, out var minAmount))
        {
            if (amount < minAmount)
            {
                result.IsValid = false;
                result.Message = $"Minimum deposit amount for {token} is {minAmount}";
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates the service fee for a deposit
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <param name="amount">Deposit amount</param>
    /// <returns>Calculated fee information</returns>
    public DepositFeeInfo CalculateDepositFee(string token, decimal amount)
    {
        var config = _depositOptions.Value.ServiceFeeConfig;
        var feeInfo = new DepositFeeInfo();

        if (!config.IsEnabled)
        {
            return feeInfo; // No fee if disabled
        }

        switch (config.FeeCalculationMethod.ToLower())
        {
            case "fixed":
                feeInfo.FeeAmount = config.FixedFees.GetValueOrDefault(token, 0);
                break;

            case "percentage":
                feeInfo.FeeAmount = amount * config.BaseFeeRate;
                break;

            case "tiered":
                feeInfo = CalculateTieredFee(amount, config.TieredFees);
                break;

            case "dynamic":
                feeInfo = CalculateDynamicFee(token, amount, config);
                break;

            default:
                feeInfo.FeeAmount = config.BaseFeeRate * amount;
                break;
        }

        // Apply minimum/maximum constraints
        if (config.TieredFees.Any())
        {
            var applicableTier = config.TieredFees
                .Where(t => amount >= t.Key)
                .OrderByDescending(t => t.Key)
                .FirstOrDefault();

            if (applicableTier.Value != null)
            {
                feeInfo.FeeAmount = Math.Max(feeInfo.FeeAmount, applicableTier.Value.MinFee);
                feeInfo.FeeAmount = Math.Min(feeInfo.FeeAmount, applicableTier.Value.MaxFee);
            }
        }

        feeInfo.FeeType = config.FeeCalculationMethod;
        return feeInfo;
    }

    /// <summary>
    /// Gets the payment address for a specific chain and token
    /// </summary>
    /// <param name="chainId">Chain identifier</param>
    /// <param name="token">Token symbol</param>
    /// <returns>Payment address or null if not found</returns>
    public string GetPaymentAddress(string chainId, string token)
    {
        var paymentAddresses = _depositOptions.Value.PaymentConfig.PaymentAddresses;
        
        if (paymentAddresses.TryGetValue(chainId, out var tokenAddresses))
        {
            return tokenAddresses.GetValueOrDefault(token);
        }

        return null;
    }

    /// <summary>
    /// Checks if a token supports swap functionality
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <returns>True if swap is supported</returns>
    public bool IsSwapSupported(string token)
    {
        return !_depositOptions.Value.PaymentConfig.NoSwapTokens.Contains(token);
    }

    /// <summary>
    /// Gets the transfer addresses for a specific network
    /// </summary>
    /// <param name="network">Network identifier</param>
    /// <returns>List of transfer addresses</returns>
    public List<string> GetTransferAddresses(string network)
    {
        var transferLists = _depositOptions.Value.AddressConfig.TransferAddressLists;
        return transferLists.GetValueOrDefault(network, new List<string>());
    }

    /// <summary>
    /// Checks if an address is whitelisted
    /// </summary>
    /// <param name="address">Address to check</param>
    /// <returns>True if address is whitelisted</returns>
    public bool IsAddressWhitelisted(string address)
    {
        return _depositOptions.Value.AddressConfig.WhitelistedAddresses.Contains(address);
    }

    /// <summary>
    /// Gets the order configuration
    /// </summary>
    /// <returns>Order configuration</returns>
    public DepositOrderConfig GetOrderConfig()
    {
        return _depositOptions.Value.OrderConfig;
    }

    /// <summary>
    /// Gets the risk configuration
    /// </summary>
    /// <returns>Risk configuration</returns>
    public DepositRiskConfig GetRiskConfig()
    {
        return _depositOptions.Value.RiskConfig;
    }

    /// <summary>
    /// Checks if risk score exceeds threshold
    /// </summary>
    /// <param name="riskScore">Risk score (0-100)</param>
    /// <returns>True if risk is too high</returns>
    public bool IsHighRisk(int riskScore)
    {
        return riskScore >= _depositOptions.Value.RiskConfig.RiskScoreThreshold;
    }

    /// <summary>
    /// Gets notification configuration
    /// </summary>
    /// <returns>Notification configuration</returns>
    public DepositNotificationConfig GetNotificationConfig()
    {
        return _depositOptions.Value.NotificationConfig;
    }

    /// <summary>
    /// Checks if a deposit amount qualifies for notifications
    /// </summary>
    /// <param name="amount">Deposit amount</param>
    /// <returns>True if notification should be sent</returns>
    public bool ShouldNotifyLargeDeposit(decimal amount)
    {
        var config = _depositOptions.Value.NotificationConfig;
        return config.NotifyLargeDeposits && amount >= config.LargeDepositThreshold;
    }

    /// <summary>
    /// Gets the maximum third-party fee for a token
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <returns>Maximum third-party fee</returns>
    public decimal GetMaxThirdPartyFee(string token)
    {
        return _depositOptions.Value.ServiceFeeConfig.MaxThirdPartyFees.GetValueOrDefault(token, 0);
    }

    /// <summary>
    /// Gets the amount threshold for a token
    /// </summary>
    /// <param name="token">Token symbol</param>
    /// <returns>Amount threshold</returns>
    public decimal GetAmountThreshold(string token)
    {
        return _depositOptions.Value.ServiceFeeConfig.AmountThresholds.GetValueOrDefault(token, 0);
    }

    #region Private Methods

    private DepositFeeInfo CalculateTieredFee(decimal amount, Dictionary<decimal, DepositFeeConfig> tieredFees)
    {
        var applicableTier = tieredFees
            .Where(t => amount >= t.Key)
            .OrderByDescending(t => t.Key)
            .FirstOrDefault();

        if (applicableTier.Value != null)
        {
            var tierConfig = applicableTier.Value;
            var feeAmount = tierConfig.FeeAmount > 0 
                ? tierConfig.FeeAmount 
                : amount * tierConfig.FeePercentage;

            return new DepositFeeInfo
            {
                FeeAmount = Math.Max(Math.Min(feeAmount, tierConfig.MaxFee), tierConfig.MinFee),
                FeeType = "tiered",
                TierThreshold = applicableTier.Key
            };
        }

        return new DepositFeeInfo();
    }

    private DepositFeeInfo CalculateDynamicFee(string token, decimal amount, DepositServiceFeeConfig config)
    {
        // Dynamic fee calculation based on market conditions, volume, etc.
        var baseFee = amount * config.BaseFeeRate;
        
        // Apply token-specific adjustments
        if (config.AmountThresholds.TryGetValue(token, out var threshold))
        {
            if (amount > threshold)
            {
                baseFee *= 0.8m; // Discount for large deposits
            }
        }

        return new DepositFeeInfo
        {
            FeeAmount = baseFee,
            FeeType = "dynamic"
        };
    }

    #endregion
}

/// <summary>
/// Result of deposit validation
/// </summary>
public class DepositValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; }
    public bool RequiresVerification { get; set; }
    public string VerificationReason { get; set; }
}

/// <summary>
/// Deposit fee calculation information
/// </summary>
public class DepositFeeInfo
{
    public decimal FeeAmount { get; set; }
    public string FeeType { get; set; }
    public decimal TierThreshold { get; set; }
    public string Description { get; set; }
} 