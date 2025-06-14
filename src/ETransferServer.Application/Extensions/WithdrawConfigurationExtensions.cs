using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ETransferServer.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Dependency injection extensions for withdraw configuration
/// </summary>
public static class WithdrawConfigurationExtensions
{
    /// <summary>
    /// Registers withdraw configuration services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration root</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddWithdrawConfiguration(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register configuration options
        services.Configure<WithdrawConfigurationOptions>(
            configuration.GetSection("WithdrawConfiguration"));

        // Register factory
        services.AddScoped<WithdrawConfigurationFactory>();

        // Register validation service
        services.AddScoped<IWithdrawConfigurationValidator, WithdrawConfigurationValidator>();

        return services;
    }

    /// <summary>
    /// Adds withdraw configuration validation
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddWithdrawConfigurationValidation(this IServiceCollection services)
    {
        services.AddOptions<WithdrawConfigurationOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) =>
            {
                var validator = serviceProvider.GetService<IWithdrawConfigurationValidator>();
                validator?.ValidateConfiguration(options);
            });

        return services;
    }
}

/// <summary>
/// Interface for withdraw configuration validation
/// </summary>
public interface IWithdrawConfigurationValidator
{
    void ValidateConfiguration(WithdrawConfigurationOptions options);
    bool IsValidConfiguration(WithdrawConfigurationOptions options, out string errorMessage);
}

/// <summary>
/// Validator for withdraw configuration
/// </summary>
public class WithdrawConfigurationValidator : IWithdrawConfigurationValidator
{
    /// <summary>
    /// Validates the entire withdraw configuration
    /// </summary>
    /// <param name="options">Configuration options to validate</param>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
    public void ValidateConfiguration(WithdrawConfigurationOptions options)
    {
        if (!IsValidConfiguration(options, out string errorMessage))
        {
            throw new ArgumentException($"Invalid withdraw configuration: {errorMessage}");
        }
    }

    /// <summary>
    /// Checks if the configuration is valid
    /// </summary>
    /// <param name="options">Configuration options to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if configuration is valid</returns>
    public bool IsValidConfiguration(WithdrawConfigurationOptions options, out string errorMessage)
    {
        errorMessage = null;

        if (options == null)
        {
            errorMessage = "WithdrawConfigurationOptions cannot be null";
            return false;
        }

        // Validate Order Configuration
        if (!ValidateOrderConfig(options.OrderConfig, out errorMessage))
            return false;

        // Validate Fee Configuration
        if (!ValidateFeeConfig(options.FeeConfig, out errorMessage))
            return false;

        // Validate Network Configuration
        if (!ValidateNetworkConfig(options.NetworkConfig, out errorMessage))
            return false;

        // Validate Third Party Configuration
        if (!ValidateThirdPartyConfig(options.ThirdPartyConfig, out errorMessage))
            return false;

        // Validate Risk Configuration
        if (!ValidateRiskConfig(options.RiskConfig, out errorMessage))
            return false;

        // Validate Notification Configuration
        if (!ValidateNotificationConfig(options.NotificationConfig, out errorMessage))
            return false;

        // Validate Performance Configuration
        if (!ValidatePerformanceConfig(options.PerformanceConfig, out errorMessage))
            return false;

        return true;
    }

    private bool ValidateOrderConfig(WithdrawOrderConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(config.OrderChangeTopic))
        {
            errorMessage = "OrderChangeTopic cannot be empty";
            return false;
        }

        if (config.ToTransferMaxRetry < 1)
        {
            errorMessage = "ToTransferMaxRetry must be at least 1";
            return false;
        }

        if (config.CallMaxRetry < 1)
        {
            errorMessage = "CallMaxRetry must be at least 1";
            return false;
        }

        if (config.CallbackMaxRetry < 1)
        {
            errorMessage = "CallbackMaxRetry must be at least 1";
            return false;
        }

        if (config.CallQueryMaxRetry < 1)
        {
            errorMessage = "CallQueryMaxRetry must be at least 1";
            return false;
        }

        if (config.MaxListLength < 100)
        {
            errorMessage = "MaxListLength should be at least 100";
            return false;
        }

        return true;
    }

    private bool ValidateFeeConfig(WithdrawFeeConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.MinWithdraw < 0)
        {
            errorMessage = "MinWithdraw cannot be negative";
            return false;
        }

        if (config.FeeFluctuationPercent < 0 || config.FeeFluctuationPercent > 1)
        {
            errorMessage = "FeeFluctuationPercent must be between 0 and 1";
            return false;
        }

        // Validate min/max fee relationships
        foreach (var minFee in config.MinThirdPartyFees)
        {
            if (config.MaxThirdPartyFees.TryGetValue(minFee.Key, out var maxFee))
            {
                if (minFee.Value > maxFee)
                {
                    errorMessage = $"MinThirdPartyFee cannot be greater than MaxThirdPartyFee for {minFee.Key}";
                    return false;
                }
            }
        }

        // Validate dynamic fee config
        if (config.DynamicFeeConfig.IsEnabled)
        {
            if (config.DynamicFeeConfig.NetworkCongestionMultiplier <= 0)
            {
                errorMessage = "NetworkCongestionMultiplier must be greater than 0";
                return false;
            }

            if (config.DynamicFeeConfig.VolumeBasedMultiplier <= 0)
            {
                errorMessage = "VolumeBasedMultiplier must be greater than 0";
                return false;
            }
        }

        return true;
    }

    private bool ValidateNetworkConfig(WithdrawNetworkConfig config, out string errorMessage)
    {
        errorMessage = null;

        // Validate network infos
        foreach (var networkInfo in config.NetworkInfos)
        {
            if (string.IsNullOrWhiteSpace(networkInfo.Coin))
            {
                errorMessage = "NetworkInfo.Coin cannot be empty";
                return false;
            }

            if (networkInfo.ConfirmNum < 0)
            {
                errorMessage = "ConfirmNum cannot be negative";
                return false;
            }

            if (networkInfo.BlockingTime < 0)
            {
                errorMessage = "BlockingTime cannot be negative";
                return false;
            }

            if (networkInfo.Decimals < 0)
            {
                errorMessage = "Decimals cannot be negative";
                return false;
            }

            if (networkInfo.FeeAlarmPercent < 0 || networkInfo.FeeAlarmPercent > 100)
            {
                errorMessage = "FeeAlarmPercent must be between 0 and 100";
                return false;
            }
        }

        // Validate transaction thresholds
        foreach (var threshold in config.TransactionThresholds.Values)
        {
            if (threshold.AmountThreshold < 0)
            {
                errorMessage = "AmountThreshold cannot be negative";
                return false;
            }

            if (threshold.BlockHeightLowerThreshold > threshold.BlockHeightUpperThreshold)
            {
                errorMessage = "BlockHeightLowerThreshold cannot be greater than BlockHeightUpperThreshold";
                return false;
            }
        }

        return true;
    }

    private bool ValidateThirdPartyConfig(WithdrawThirdPartyConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.ThirdPartCacheFeeExpireSeconds < 1)
        {
            errorMessage = "ThirdPartCacheFeeExpireSeconds must be at least 1";
            return false;
        }

        if (config.ThirdPartFeeExpireSeconds < 1)
        {
            errorMessage = "ThirdPartFeeExpireSeconds must be at least 1";
            return false;
        }

        if (config.WithdrawThreshold < 0)
        {
            errorMessage = "WithdrawThreshold cannot be negative";
            return false;
        }

        // Validate service endpoints
        foreach (var endpoint in config.ServiceEndpoints.Values)
        {
            if (string.IsNullOrWhiteSpace(endpoint.BaseUrl))
            {
                errorMessage = "ServiceEndpoint.BaseUrl cannot be empty";
                return false;
            }

            if (endpoint.TimeoutSeconds < 1)
            {
                errorMessage = "ServiceEndpoint.TimeoutSeconds must be at least 1";
                return false;
            }

            if (endpoint.MaxRetries < 0)
            {
                errorMessage = "ServiceEndpoint.MaxRetries cannot be negative";
                return false;
            }
        }

        // Validate rate limit config
        var rateLimitConfig = config.RateLimitConfig;
        if (rateLimitConfig.RequestsPerMinute < 1)
        {
            errorMessage = "RequestsPerMinute must be at least 1";
            return false;
        }

        if (rateLimitConfig.BurstLimit < 1)
        {
            errorMessage = "BurstLimit must be at least 1";
            return false;
        }

        if (rateLimitConfig.CooldownSeconds < 0)
        {
            errorMessage = "CooldownSeconds cannot be negative";
            return false;
        }

        return true;
    }

    private bool ValidateRiskConfig(WithdrawRiskConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.MaxWithdrawWithoutVerification <= 0)
        {
            errorMessage = "MaxWithdrawWithoutVerification must be greater than 0";
            return false;
        }

        if (config.DailyWithdrawLimit <= 0)
        {
            errorMessage = "DailyWithdrawLimit must be greater than 0";
            return false;
        }

        if (config.RiskScoreThreshold < 0 || config.RiskScoreThreshold > 100)
        {
            errorMessage = "RiskScoreThreshold must be between 0 and 100";
            return false;
        }

        // Validate velocity config
        var velocityConfig = config.VelocityConfig;
        if (velocityConfig.IsEnabled)
        {
            if (velocityConfig.MaxWithdrawalsPerHour < 1)
            {
                errorMessage = "MaxWithdrawalsPerHour must be at least 1";
                return false;
            }

            if (velocityConfig.MaxAmountPerHour <= 0)
            {
                errorMessage = "MaxAmountPerHour must be greater than 0";
                return false;
            }

            if (velocityConfig.TimeWindowMinutes < 1)
            {
                errorMessage = "TimeWindowMinutes must be at least 1";
                return false;
            }
        }

        return true;
    }

    private bool ValidateNotificationConfig(WithdrawNotificationConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.LargeWithdrawalThreshold <= 0)
        {
            errorMessage = "LargeWithdrawalThreshold must be greater than 0";
            return false;
        }

        if (config.FeeFluctuationThreshold < 0 || config.FeeFluctuationThreshold > 1)
        {
            errorMessage = "FeeFluctuationThreshold must be between 0 and 1";
            return false;
        }

        if (config.NotificationCooldownSeconds < 0)
        {
            errorMessage = "NotificationCooldownSeconds cannot be negative";
            return false;
        }

        // Validate Slack config
        var slackConfig = config.SlackConfig;
        if (slackConfig.IsEnabled && string.IsNullOrWhiteSpace(slackConfig.WebhookUrl))
        {
            errorMessage = "SlackConfig.WebhookUrl cannot be empty when Slack is enabled";
            return false;
        }

        return true;
    }

    private bool ValidatePerformanceConfig(WithdrawPerformanceConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.MaxBatchSize < 1)
        {
            errorMessage = "MaxBatchSize must be at least 1";
            return false;
        }

        if (config.BatchIntervalSeconds < 1)
        {
            errorMessage = "BatchIntervalSeconds must be at least 1";
            return false;
        }

        if (config.CacheExpirationSeconds < 1)
        {
            errorMessage = "CacheExpirationSeconds must be at least 1";
            return false;
        }

        // Validate thread pool config
        var threadPoolConfig = config.ThreadPoolConfig;
        if (threadPoolConfig.MinWorkerThreads < 1)
        {
            errorMessage = "MinWorkerThreads must be at least 1";
            return false;
        }

        if (threadPoolConfig.MaxWorkerThreads < threadPoolConfig.MinWorkerThreads)
        {
            errorMessage = "MaxWorkerThreads cannot be less than MinWorkerThreads";
            return false;
        }

        if (threadPoolConfig.QueueCapacity < 1)
        {
            errorMessage = "QueueCapacity must be at least 1";
            return false;
        }

        return true;
    }
} 