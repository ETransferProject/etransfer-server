using System.Collections.Generic;

namespace ETransferServer.Grains.Options;

/// <summary>
/// Network configuration options for Grains layer services
/// Handles external service integrations and blockchain-specific operations
/// </summary>
public class NetworkOptions
{
    /// <summary>
    /// CoBo wallet integration configuration
    /// Key: Configuration parameter name (e.g., "ApiKey", "BaseUrl", "Environment")
    /// Value: Configuration value
    /// Used for managing CoBo wallet API endpoints, authentication, and operational settings
    /// </summary>
    public Dictionary<string, string> CoBo { get; set; } = new();
    
    /// <summary>
    /// Generic network provider configurations
    /// Key: Provider name (e.g., "Infura", "Alchemy", "QuickNode")
    /// Value: Provider-specific configuration dictionary
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> NetworkProviders { get; set; } = new();
    
    /// <summary>
    /// Network monitoring and health check configurations
    /// Key: Network identifier, Value: Health check settings
    /// </summary>
    public Dictionary<string, NetworkHealthConfig> HealthCheck { get; set; } = new();
    
    /// <summary>
    /// Retry policies for network operations
    /// </summary>
    public NetworkRetryConfig RetryPolicy { get; set; } = new();
}

/// <summary>
/// Health check configuration for network monitoring
/// </summary>
public class NetworkHealthConfig
{
    /// <summary>
    /// Whether health checks are enabled for this network
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Interval in seconds between health checks
    /// Default: 30 seconds
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Timeout in seconds for health check requests
    /// Default: 10 seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
    
    /// <summary>
    /// Number of consecutive failures before marking network as unhealthy
    /// Default: 3
    /// </summary>
    public int FailureThreshold { get; set; } = 3;
    
    /// <summary>
    /// Health check endpoint URL
    /// </summary>
    public string HealthCheckUrl { get; set; }
}

/// <summary>
/// Retry policy configuration for network operations
/// </summary>
public class NetworkRetryConfig
{
    /// <summary>
    /// Maximum number of retry attempts
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    
    /// <summary>
    /// Base delay in milliseconds between retries
    /// Default: 1000ms (1 second)
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// Multiplier for exponential backoff
    /// Default: 2.0 (double the delay each retry)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
    
    /// <summary>
    /// Maximum delay in milliseconds between retries
    /// Default: 30000ms (30 seconds)
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;
    
    /// <summary>
    /// HTTP status codes that should trigger a retry
    /// Default: [500, 502, 503, 504, 429]
    /// </summary>
    public List<int> RetryOnStatusCodes { get; set; } = new() { 500, 502, 503, 504, 429 };
}