using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ETransferServer.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Dependency injection extensions for deposit configuration
/// </summary>
public static class DepositConfigurationExtensions
{
    /// <summary>
    /// Registers deposit configuration services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration root</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDepositConfiguration(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register configuration options
        services.Configure<DepositConfigurationOptions>(
            configuration.GetSection("DepositConfiguration"));

        // Register factory
        services.AddScoped<DepositConfigurationFactory>();

        // Register validation service
        services.AddScoped<IDepositConfigurationValidator, DepositConfigurationValidator>();

        return services;
    }

    /// <summary>
    /// Adds deposit configuration validation
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddDepositConfigurationValidation(this IServiceCollection services)
    {
        services.AddOptions<DepositConfigurationOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) =>
            {
                var validator = serviceProvider.GetService<IDepositConfigurationValidator>();
                validator?.ValidateConfiguration(options);
            });

        return services;
    }
}

/// <summary>
/// Interface for deposit configuration validation
/// </summary>
public interface IDepositConfigurationValidator
{
    void ValidateConfiguration(DepositConfigurationOptions options);
    bool IsValidConfiguration(DepositConfigurationOptions options, out string errorMessage);
}

/// <summary>
/// Validator for deposit configuration
/// </summary>
public class DepositConfigurationValidator : IDepositConfigurationValidator
{
    /// <summary>
    /// Validates the entire deposit configuration
    /// </summary>
    /// <param name="options">Configuration options to validate</param>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
    public void ValidateConfiguration(DepositConfigurationOptions options)
    {
        if (!IsValidConfiguration(options, out string errorMessage))
        {
            throw new ArgumentException($"Invalid deposit configuration: {errorMessage}");
        }
    }

    /// <summary>
    /// Checks if the configuration is valid
    /// </summary>
    /// <param name="options">Configuration options to validate</param>
    /// <param name="errorMessage">Error message if validation fails</param>
    /// <returns>True if configuration is valid</returns>
    public bool IsValidConfiguration(DepositConfigurationOptions options, out string errorMessage)
    {
        errorMessage = null;

        if (options == null)
        {
            errorMessage = "DepositConfigurationOptions cannot be null";
            return false;
        }

        // Validate Order Configuration
        if (!ValidateOrderConfig(options.OrderConfig, out errorMessage))
            return false;

        // Validate Service Fee Configuration
        if (!ValidateServiceFeeConfig(options.ServiceFeeConfig, out errorMessage))
            return false;

        // Validate Address Configuration
        if (!ValidateAddressConfig(options.AddressConfig, out errorMessage))
            return false;

        // Validate Payment Configuration
        if (!ValidatePaymentConfig(options.PaymentConfig, out errorMessage))
            return false;

        // Validate Risk Configuration
        if (!ValidateRiskConfig(options.RiskConfig, out errorMessage))
            return false;

        // Validate Notification Configuration
        if (!ValidateNotificationConfig(options.NotificationConfig, out errorMessage))
            return false;

        return true;
    }

    private bool ValidateOrderConfig(DepositOrderConfig config, out string errorMessage)
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

        if (config.MaxListLength < 100)
        {
            errorMessage = "MaxListLength should be at least 100";
            return false;
        }

        if (config.AssignedAddressExpiredHour < 1)
        {
            errorMessage = "AssignedAddressExpiredHour must be at least 1 hour";
            return false;
        }

        return true;
    }

    private bool ValidateServiceFeeConfig(DepositServiceFeeConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.BaseFeeRate < 0 || config.BaseFeeRate > 1)
        {
            errorMessage = "BaseFeeRate must be between 0 and 1";
            return false;
        }

        var validMethods = new[] { "fixed", "percentage", "dynamic", "tiered" };
        if (!Array.Exists(validMethods, method => 
            string.Equals(method, config.FeeCalculationMethod, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = $"FeeCalculationMethod must be one of: {string.Join(", ", validMethods)}";
            return false;
        }

        // Validate tiered fees structure
        foreach (var tier in config.TieredFees)
        {
            if (tier.Value.MinFee > tier.Value.MaxFee)
            {
                errorMessage = $"MinFee cannot be greater than MaxFee for tier {tier.Key}";
                return false;
            }
        }

        return true;
    }

    private bool ValidateAddressConfig(DepositAddressConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.RemainingThreshold < 1)
        {
            errorMessage = "RemainingThreshold must be at least 1";
            return false;
        }

        if (config.MaxRequestNewAddressCount < 1)
        {
            errorMessage = "MaxRequestNewAddressCount must be at least 1";
            return false;
        }

        if (config.MaxRequestNewAddressRetry < 1)
        {
            errorMessage = "MaxRequestNewAddressRetry must be at least 1";
            return false;
        }

        return true;
    }

    private bool ValidatePaymentConfig(DepositPaymentConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.PaymentTimeoutSeconds < 30)
        {
            errorMessage = "PaymentTimeoutSeconds should be at least 30 seconds";
            return false;
        }

        if (config.MaxBatchSize < 1)
        {
            errorMessage = "MaxBatchSize must be at least 1";
            return false;
        }

        return true;
    }

    private bool ValidateRiskConfig(DepositRiskConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.MaxDepositWithoutVerification <= 0)
        {
            errorMessage = "MaxDepositWithoutVerification must be greater than 0";
            return false;
        }

        if (config.DailyDepositLimit <= 0)
        {
            errorMessage = "DailyDepositLimit must be greater than 0";
            return false;
        }

        if (config.RiskScoreThreshold < 0 || config.RiskScoreThreshold > 100)
        {
            errorMessage = "RiskScoreThreshold must be between 0 and 100";
            return false;
        }

        return true;
    }

    private bool ValidateNotificationConfig(DepositNotificationConfig config, out string errorMessage)
    {
        errorMessage = null;

        if (config.LargeDepositThreshold <= 0)
        {
            errorMessage = "LargeDepositThreshold must be greater than 0";
            return false;
        }

        if (config.NotificationCooldownSeconds < 0)
        {
            errorMessage = "NotificationCooldownSeconds cannot be negative";
            return false;
        }

        return true;
    }
} 