using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ETransferServer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Token configuration extensions for dependency injection and validation
/// </summary>
public static class TokenConfigurationExtensions
{
    /// <summary>
    /// Add token configuration services to the service collection
    /// </summary>
    public static IServiceCollection AddTokenConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure token options
        services.Configure<TokenConfigurationOptions>(
            configuration.GetSection("TokenConfiguration"));
        
        // Register factory
        services.AddSingleton<TokenConfigurationFactory>();

        return services;
    }

    /// <summary>
    /// Validates the token configuration
    /// </summary>
    public static ValidationResult ValidateConfiguration(this TokenConfigurationOptions options)
    {
        var result = new ValidationResult();
        
        // Basic validation
        if (options.CoreConfig?.GlobalTokenRegistry == null || !options.CoreConfig.GlobalTokenRegistry.Any())
        {
            result.AddError("No token definitions found");
        }
        
        return result;
    }

    /// <summary>
    /// Validates access configuration
    /// </summary>
    public static ValidationResult ValidateAccessConfig(this TokenAccessConfig config)
    {
        var result = new ValidationResult();
        
        if (config.ReApplyHours <= 0)
        {
            result.AddError("ReApplyHours must be greater than 0");
        }
        
        return result;
    }

    /// <summary>
    /// Validates security configuration
    /// </summary>
    public static ValidationResult ValidateSecurityConfig(this TokenSecurityConfig config)
    {
        var result = new ValidationResult();
        
        if (config.ContractSecurity == null)
        {
            result.AddError("Contract security configuration is required");
        }
        
        return result;
    }

    /// <summary>
    /// Validate token configuration on startup
    /// </summary>
    public static void ValidateTokenConfiguration(
        this IServiceProvider serviceProvider,
        ILogger logger)
    {
        try
        {
            var tokenOptions = serviceProvider.GetRequiredService<IOptions<TokenConfigurationOptions>>();
            var validationResult = tokenOptions.Value.ValidateConfiguration();
            
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    logger.LogError("Token configuration validation error: {Error}", error);
                }
                throw new InvalidOperationException("Token configuration validation failed");
            }
            
            logger.LogInformation("Token configuration validation passed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate token configuration");
            throw;
        }
    }
}

/// <summary>
/// Validation result for configuration validation
/// </summary>
public class ValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    
    /// <summary>
    /// Whether the validation passed
    /// </summary>
    public bool IsValid => !_errors.Any();
    
    /// <summary>
    /// List of validation errors
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();
    
    /// <summary>
    /// List of validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();
    
    /// <summary>
    /// Add an error to the validation result
    /// </summary>
    public void AddError(string error)
    {
        _errors.Add(error);
    }
    
    /// <summary>
    /// Add a warning to the validation result
    /// </summary>
    public void AddWarning(string warning)
    {
        _warnings.Add(warning);
    }
} 