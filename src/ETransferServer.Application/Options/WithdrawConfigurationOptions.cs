using System.Collections.Generic;

namespace ETransferServer.Options;

/// <summary>
/// Unified configuration options for all withdraw-related operations
/// Consolidates withdraw settings, fee configurations, and network management
/// </summary>
public class WithdrawConfigurationOptions
{
    /// <summary>
    /// Order processing configuration
    /// </summary>
    public WithdrawOrderConfig OrderConfig { get; set; } = new();
    
    /// <summary>
    /// Fee calculation and management configuration
    /// </summary>
    public WithdrawFeeConfig FeeConfig { get; set; } = new();
    
    /// <summary>
    /// Network-specific configuration
    /// </summary>
    public WithdrawNetworkConfig NetworkConfig { get; set; } = new();
    
    /// <summary>
    /// Third-party service configuration
    /// </summary>
    public WithdrawThirdPartyConfig ThirdPartyConfig { get; set; } = new();
    
    /// <summary>
    /// Risk management configuration
    /// </summary>
    public WithdrawRiskConfig RiskConfig { get; set; } = new();
    
    /// <summary>
    /// Notification and monitoring configuration
    /// </summary>
    public WithdrawNotificationConfig NotificationConfig { get; set; } = new();
    
    /// <summary>
    /// Performance and optimization configuration
    /// </summary>
    public WithdrawPerformanceConfig PerformanceConfig { get; set; } = new();
}

/// <summary>
/// Order processing configuration for withdrawals
/// Controls order lifecycle, retries, and processing limits
/// </summary>
public class WithdrawOrderConfig
{
    /// <summary>
    /// Whether withdrawal operations are globally enabled
    /// Default: true
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Kafka topic for withdraw order change notifications
    /// </summary>
    public string OrderChangeTopic { get; set; } = "ETransfer-Withdraw-OrderChange";
    
    /// <summary>
    /// Maximum retry attempts for failed transfer operations
    /// Default: 5
    /// </summary>
    public int ToTransferMaxRetry { get; set; } = 5;
    
    /// <summary>
    /// Maximum retry attempts for API calls
    /// Default: 5
    /// </summary>
    public int CallMaxRetry { get; set; } = 5;
    
    /// <summary>
    /// Maximum retry attempts for callback operations
    /// Default: 5
    /// </summary>
    public int CallbackMaxRetry { get; set; } = 5;
    
    /// <summary>
    /// Maximum retry attempts for query operations
    /// Default: 5
    /// </summary>
    public int CallQueryMaxRetry { get; set; } = 5;
    
    /// <summary>
    /// Maximum number of items in processing lists
    /// Used to prevent memory issues with large datasets
    /// Default: 1000
    /// </summary>
    public int MaxListLength { get; set; } = 1000;
    
    /// <summary>
    /// Whether cross-chain withdrawals on the same chain are allowed
    /// Default: false
    /// </summary>
    public bool CanCrossSameChain { get; set; } = false;
    
    /// <summary>
    /// Payment addresses for different chains and tokens
    /// Key: Chain identifier, Value: Dictionary of token to address mappings
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
}

/// <summary>
/// Fee calculation and management configuration for withdrawals
/// Manages fee structures, calculations, and fluctuation controls
/// </summary>
public class WithdrawFeeConfig
{
    /// <summary>
    /// Global minimum withdrawal amount
    /// Default: 0.2
    /// </summary>
    public decimal MinWithdraw { get; set; } = 0.2m;
    
    /// <summary>
    /// Allowed fee fluctuation percentage (0.1 = 10%)
    /// Used for fee stability and user experience
    /// Default: 0.1 (10%)
    /// </summary>
    public decimal FeeFluctuationPercent { get; set; } = 0.1m;
    
    /// <summary>
    /// Minimum third-party fees per token/network combination
    /// Key: Network_Token format (e.g., "ETH_USDT"), Value: Minimum fee amount
    /// </summary>
    public Dictionary<string, decimal> MinThirdPartyFees { get; set; } = new();
    
    /// <summary>
    /// Maximum third-party fees per token/network combination
    /// Key: Network_Token format (e.g., "ETH_USDT"), Value: Maximum fee amount
    /// </summary>
    public Dictionary<string, decimal> MaxThirdPartyFees { get; set; } = new();
    
    /// <summary>
    /// Large amount thresholds for special handling
    /// Key: Token symbol, Value: Threshold amount
    /// </summary>
    public Dictionary<string, decimal> LargeAmountThresholds { get; set; } = new();
    
    /// <summary>
    /// Token information including decimal places
    /// Key: Token symbol, Value: Decimal places
    /// </summary>
    public Dictionary<string, int> TokenDecimals { get; set; } = new()
    {
        ["USDT"] = 6,
        ["SGR-1"] = 8,
        ["ELF"] = 8
    };
    
    /// <summary>
    /// Dynamic fee calculation configuration
    /// </summary>
    public WithdrawDynamicFeeConfig DynamicFeeConfig { get; set; } = new();
}

/// <summary>
/// Dynamic fee calculation configuration
/// </summary>
public class WithdrawDynamicFeeConfig
{
    /// <summary>
    /// Whether dynamic fee calculation is enabled
    /// Default: false
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Base fee multiplier for network congestion
    /// Default: 1.0
    /// </summary>
    public decimal NetworkCongestionMultiplier { get; set; } = 1.0m;
    
    /// <summary>
    /// Fee adjustment based on transaction volume
    /// Default: 1.0
    /// </summary>
    public decimal VolumeBasedMultiplier { get; set; } = 1.0m;
    
    /// <summary>
    /// Time-based fee adjustments (peak hours)
    /// Key: Hour (0-23), Value: Multiplier
    /// </summary>
    public Dictionary<int, decimal> TimeBasedMultipliers { get; set; } = new();
}

/// <summary>
/// Network-specific configuration for withdrawals
/// Controls network behavior, confirmations, and timing
/// </summary>
public class WithdrawNetworkConfig
{
    /// <summary>
    /// Network information for different coins
    /// </summary>
    public List<WithdrawNetworkInfo> NetworkInfos { get; set; } = new();
    
    /// <summary>
    /// Homogeneous transaction thresholds per token
    /// Key: Token symbol, Value: Transaction threshold configuration
    /// </summary>
    public Dictionary<string, WithdrawTransactionThreshold> TransactionThresholds { get; set; } = new();
    
    /// <summary>
    /// Transfer paths for cross-chain operations
    /// Key: Route identifier, Value: List of intermediate steps
    /// </summary>
    public Dictionary<string, List<string>> TransferPaths { get; set; } = new();
    
    /// <summary>
    /// Support whitelists per network
    /// Key: Network identifier, Value: List of whitelisted addresses/patterns
    /// </summary>
    public Dictionary<string, List<string>> SupportWhiteLists { get; set; } = new();
}

/// <summary>
/// Network information for withdraw operations
/// </summary>
public class WithdrawNetworkInfo
{
    /// <summary>
    /// Coin identifier in Network_Token format
    /// </summary>
    public string Coin { get; set; }
    
    /// <summary>
    /// Number of confirmations required
    /// </summary>
    public int ConfirmNum { get; set; }
    
    /// <summary>
    /// Blocking time in minutes before processing
    /// </summary>
    public decimal BlockingTime { get; set; }
    
    /// <summary>
    /// Extra request time in seconds for additional processing
    /// Default: 30
    /// </summary>
    public int ExtraRequestTime { get; set; } = 30;
    
    /// <summary>
    /// Decimal places for this coin
    /// </summary>
    public int Decimals { get; set; }
    
    /// <summary>
    /// Default amount for operations
    /// Default: 0
    /// </summary>
    public decimal Amount { get; set; } = 0;
    
    /// <summary>
    /// Fee alarm threshold percentage
    /// Default: 10%
    /// </summary>
    public decimal FeeAlarmPercent { get; set; } = 10;
    
    /// <summary>
    /// Estimated arrival time in seconds
    /// Default: 1000
    /// </summary>
    public int EstimatedArrivalTime { get; set; } = 1000;
}

/// <summary>
/// Transaction threshold configuration for homogeneous operations
/// </summary>
public class WithdrawTransactionThreshold
{
    /// <summary>
    /// Amount threshold for special processing
    /// Default: 300
    /// </summary>
    public long AmountThreshold { get; set; } = 300;
    
    /// <summary>
    /// Upper block height threshold
    /// Default: 300
    /// </summary>
    public long BlockHeightUpperThreshold { get; set; } = 300;
    
    /// <summary>
    /// Lower block height threshold
    /// Default: 30
    /// </summary>
    public long BlockHeightLowerThreshold { get; set; } = 30;
    
    /// <summary>
    /// Specific withdraw fee for this threshold
    /// Default: 0
    /// </summary>
    public decimal WithdrawFee { get; set; } = 0m;
}

/// <summary>
/// Third-party service configuration for withdrawals
/// Manages external service integrations and caching
/// </summary>
public class WithdrawThirdPartyConfig
{
    /// <summary>
    /// Cache expiration time for third-party fees in seconds
    /// Default: 180 seconds (3 minutes)
    /// </summary>
    public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;
    
    /// <summary>
    /// Third-party fee expiration time in seconds
    /// Default: 180 seconds (3 minutes)
    /// </summary>
    public int ThirdPartFeeExpireSeconds { get; set; } = 180;
    
    /// <summary>
    /// Withdrawal threshold for third-party processing
    /// Default: 100000
    /// </summary>
    public long WithdrawThreshold { get; set; } = 100000;
    
    /// <summary>
    /// Third-party service endpoints
    /// Key: Service name, Value: Endpoint configuration
    /// </summary>
    public Dictionary<string, WithdrawServiceEndpoint> ServiceEndpoints { get; set; } = new();
    
    /// <summary>
    /// API rate limiting configuration
    /// </summary>
    public WithdrawRateLimitConfig RateLimitConfig { get; set; } = new();
}

/// <summary>
/// Service endpoint configuration
/// </summary>
public class WithdrawServiceEndpoint
{
    /// <summary>
    /// Base URL for the service
    /// </summary>
    public string BaseUrl { get; set; }
    
    /// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; }
    
    /// <summary>
    /// Request timeout in seconds
    /// Default: 30
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum retry attempts for this service
    /// Default: 3
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Rate limiting configuration for API calls
/// </summary>
public class WithdrawRateLimitConfig
{
    /// <summary>
    /// Maximum requests per minute
    /// Default: 60
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Burst limit for short-term spikes
    /// Default: 10
    /// </summary>
    public int BurstLimit { get; set; } = 10;
    
    /// <summary>
    /// Cooldown period in seconds after hitting limits
    /// Default: 60
    /// </summary>
    public int CooldownSeconds { get; set; } = 60;
}

/// <summary>
/// Risk management configuration for withdrawals
/// Controls fraud detection, limits, and security measures
/// </summary>
public class WithdrawRiskConfig
{
    /// <summary>
    /// Whether risk management is enabled
    /// Default: true
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Maximum withdrawal amount per transaction without additional verification
    /// Default: 50000
    /// </summary>
    public decimal MaxWithdrawWithoutVerification { get; set; } = 50000m;
    
    /// <summary>
    /// Maximum withdrawals per user per day
    /// Default: 500000
    /// </summary>
    public decimal DailyWithdrawLimit { get; set; } = 500000m;
    
    /// <summary>
    /// Whether to enable real-time fraud detection
    /// Default: true
    /// </summary>
    public bool EnableFraudDetection { get; set; } = true;
    
    /// <summary>
    /// Risk score threshold for blocking withdrawals
    /// Range: 0-100, Default: 85
    /// </summary>
    public int RiskScoreThreshold { get; set; } = 85;
    
    /// <summary>
    /// Countries/regions that are blocked from withdrawals
    /// </summary>
    public List<string> BlockedRegions { get; set; } = new();
    
    /// <summary>
    /// Suspicious address patterns to flag
    /// </summary>
    public List<string> SuspiciousAddressPatterns { get; set; } = new();
    
    /// <summary>
    /// Velocity check configuration
    /// </summary>
    public WithdrawVelocityConfig VelocityConfig { get; set; } = new();
}

/// <summary>
/// Velocity check configuration for withdrawal risk management
/// </summary>
public class WithdrawVelocityConfig
{
    /// <summary>
    /// Whether velocity checks are enabled
    /// Default: true
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Maximum number of withdrawals per hour
    /// Default: 10
    /// </summary>
    public int MaxWithdrawalsPerHour { get; set; } = 10;
    
    /// <summary>
    /// Maximum withdrawal amount per hour
    /// Default: 100000
    /// </summary>
    public decimal MaxAmountPerHour { get; set; } = 100000m;
    
    /// <summary>
    /// Time window for velocity checks in minutes
    /// Default: 60 minutes
    /// </summary>
    public int TimeWindowMinutes { get; set; } = 60;
}

/// <summary>
/// Notification and monitoring configuration for withdrawals
/// Manages alerts, monitoring, and notification channels
/// </summary>
public class WithdrawNotificationConfig
{
    /// <summary>
    /// Whether to send notifications for large withdrawals
    /// Default: true
    /// </summary>
    public bool NotifyLargeWithdrawals { get; set; } = true;
    
    /// <summary>
    /// Threshold amount for large withdrawal notifications
    /// Default: 100000
    /// </summary>
    public decimal LargeWithdrawalThreshold { get; set; } = 100000m;
    
    /// <summary>
    /// Whether to send notifications for failed withdrawals
    /// Default: true
    /// </summary>
    public bool NotifyFailedWithdrawals { get; set; } = true;
    
    /// <summary>
    /// Whether to send fee fluctuation alerts
    /// Default: true
    /// </summary>
    public bool NotifyFeeFluctuations { get; set; } = true;
    
    /// <summary>
    /// Fee fluctuation threshold for notifications (percentage)
    /// Default: 20%
    /// </summary>
    public decimal FeeFluctuationThreshold { get; set; } = 0.2m;
    
    /// <summary>
    /// Email addresses for withdrawal notifications
    /// </summary>
    public List<string> NotificationEmails { get; set; } = new();
    
    /// <summary>
    /// Webhook URLs for withdrawal notifications
    /// </summary>
    public List<string> NotificationWebhooks { get; set; } = new();
    
    /// <summary>
    /// Minimum time between similar notifications (in seconds)
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int NotificationCooldownSeconds { get; set; } = 300;
    
    /// <summary>
    /// Slack/Teams integration configuration
    /// </summary>
    public WithdrawSlackConfig SlackConfig { get; set; } = new();
}

/// <summary>
/// Slack integration configuration for notifications
/// </summary>
public class WithdrawSlackConfig
{
    /// <summary>
    /// Whether Slack notifications are enabled
    /// Default: false
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Slack webhook URL
    /// </summary>
    public string WebhookUrl { get; set; }
    
    /// <summary>
    /// Default channel for notifications
    /// </summary>
    public string DefaultChannel { get; set; } = "#withdrawals";
    
    /// <summary>
    /// Bot username for notifications
    /// </summary>
    public string BotUsername { get; set; } = "ETransfer-Withdraw-Bot";
}

/// <summary>
/// Performance and optimization configuration for withdrawals
/// Controls caching, batching, and performance optimizations
/// </summary>
public class WithdrawPerformanceConfig
{
    /// <summary>
    /// Whether to enable batch processing
    /// Default: true
    /// </summary>
    public bool EnableBatchProcessing { get; set; } = true;
    
    /// <summary>
    /// Maximum batch size for processing
    /// Default: 100
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
    
    /// <summary>
    /// Batch processing interval in seconds
    /// Default: 30
    /// </summary>
    public int BatchIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Whether to enable result caching
    /// Default: true
    /// </summary>
    public bool EnableCaching { get; set; } = true;
    
    /// <summary>
    /// Cache expiration time in seconds
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int CacheExpirationSeconds { get; set; } = 300;
    
    /// <summary>
    /// Whether to enable async processing
    /// Default: true
    /// </summary>
    public bool EnableAsyncProcessing { get; set; } = true;
    
    /// <summary>
    /// Thread pool configuration
    /// </summary>
    public WithdrawThreadPoolConfig ThreadPoolConfig { get; set; } = new();
}

/// <summary>
/// Thread pool configuration for withdraw processing
/// </summary>
public class WithdrawThreadPoolConfig
{
    /// <summary>
    /// Minimum number of worker threads
    /// Default: 2
    /// </summary>
    public int MinWorkerThreads { get; set; } = 2;
    
    /// <summary>
    /// Maximum number of worker threads
    /// Default: 10
    /// </summary>
    public int MaxWorkerThreads { get; set; } = 10;
    
    /// <summary>
    /// Queue capacity for pending tasks
    /// Default: 1000
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;
} 