using System.Collections.Generic;

namespace ETransferServer.Options;

/// <summary>
/// Unified configuration options for all deposit-related operations
/// Consolidates deposit settings, fee configurations, and address management
/// </summary>
public class DepositConfigurationOptions
{
    /// <summary>
    /// Order processing configuration
    /// </summary>
    public DepositOrderConfig OrderConfig { get; set; } = new();
    
    /// <summary>
    /// Service fee configuration for deposits
    /// </summary>
    public DepositServiceFeeConfig ServiceFeeConfig { get; set; } = new();
    
    /// <summary>
    /// Address management configuration
    /// </summary>
    public DepositAddressConfig AddressConfig { get; set; } = new();
    
    /// <summary>
    /// Payment processing configuration
    /// </summary>
    public DepositPaymentConfig PaymentConfig { get; set; } = new();
    
    /// <summary>
    /// Risk management configuration
    /// </summary>
    public DepositRiskConfig RiskConfig { get; set; } = new();
    
    /// <summary>
    /// Notification and alarm configuration
    /// </summary>
    public DepositNotificationConfig NotificationConfig { get; set; } = new();
}

/// <summary>
/// Order processing configuration for deposits
/// Controls order lifecycle, retries, and processing limits
/// </summary>
public class DepositOrderConfig
{
    /// <summary>
    /// Kafka topic for order change notifications
    /// </summary>
    public string OrderChangeTopic { get; set; } = "ETransfer-Deposit-OrderChange";
    
    /// <summary>
    /// Maximum retry attempts for failed transfer operations
    /// Default: 5
    /// </summary>
    public int ToTransferMaxRetry { get; set; } = 5;
    
    /// <summary>
    /// Maximum number of items in processing lists
    /// Used to prevent memory issues with large datasets
    /// Default: 1000
    /// </summary>
    public int MaxListLength { get; set; } = 1000;
    
    /// <summary>
    /// Duration in hours after which assigned addresses expire
    /// Default: 48 hours
    /// </summary>
    public int AssignedAddressExpiredHour { get; set; } = 48;
    
    /// <summary>
    /// Transaction pair type mappings for multi-token swaps
    /// Key: Transaction type identifier, Value: Token pair (e.g., "USDT_SGR-1")
    /// </summary>
    public Dictionary<string, string> TxPairType { get; set; } = new()
    {
        ["0"] = "USDT_SGR-1"
    };
}

/// <summary>
/// Service fee configuration for deposit operations
/// Manages fee calculations, thresholds, and service fee enablement
/// </summary>
public class DepositServiceFeeConfig
{
    /// <summary>
    /// Whether service fees are enabled globally
    /// Default: false
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Fee calculation method
    /// Values: "fixed", "percentage", "dynamic", "tiered"
    /// Default: "fixed"
    /// </summary>
    public string FeeCalculationMethod { get; set; } = "fixed";
    
    /// <summary>
    /// Amount thresholds for fee calculation per token
    /// Key: Token symbol, Value: Threshold amount
    /// </summary>
    public Dictionary<string, decimal> AmountThresholds { get; set; } = new();
    
    /// <summary>
    /// Maximum third-party fees allowed per token
    /// Key: Token symbol, Value: Maximum fee amount
    /// </summary>
    public Dictionary<string, decimal> MaxThirdPartyFees { get; set; } = new();
    
    /// <summary>
    /// Minimum deposit amounts per token
    /// Key: Token symbol, Value: Minimum amount
    /// </summary>
    public Dictionary<string, decimal> MinDepositAmounts { get; set; } = new();
    
    /// <summary>
    /// Base service fee percentage (0.01 = 1%)
    /// Default: 0.001 (0.1%)
    /// </summary>
    public decimal BaseFeeRate { get; set; } = 0.001m;
    
    /// <summary>
    /// Fixed fee amounts per token
    /// Key: Token symbol, Value: Fixed fee amount
    /// </summary>
    public Dictionary<string, decimal> FixedFees { get; set; } = new();
    
    /// <summary>
    /// Tiered fee structure based on deposit amounts
    /// Key: Amount threshold, Value: Fee configuration
    /// </summary>
    public Dictionary<decimal, DepositFeeConfig> TieredFees { get; set; } = new();
}

/// <summary>
/// Fee configuration for specific deposit amounts or tiers
/// </summary>
public class DepositFeeConfig
{
    /// <summary>
    /// Fee amount for this tier
    /// </summary>
    public decimal FeeAmount { get; set; }
    
    /// <summary>
    /// Fee percentage for this tier (0.01 = 1%)
    /// </summary>
    public decimal FeePercentage { get; set; }
    
    /// <summary>
    /// Minimum fee for this tier
    /// </summary>
    public decimal MinFee { get; set; }
    
    /// <summary>
    /// Maximum fee for this tier
    /// </summary>
    public decimal MaxFee { get; set; }
}

/// <summary>
/// Address management configuration for deposits
/// Controls address generation, assignment, and lifecycle management
/// </summary>
public class DepositAddressConfig
{
    /// <summary>
    /// Threshold for remaining addresses before generating new ones
    /// Default: 50
    /// </summary>
    public int RemainingThreshold { get; set; } = 50;
    
    /// <summary>
    /// Maximum number of new addresses to request in a single batch
    /// Default: 2
    /// </summary>
    public int MaxRequestNewAddressCount { get; set; } = 2;
    
    /// <summary>
    /// Maximum assigned addresses before triggering transfer
    /// Default: 200
    /// </summary>
    public int MaxAssignedTransferThreshold { get; set; } = 200;
    
    /// <summary>
    /// Maximum retry attempts for new address requests
    /// Default: 3
    /// </summary>
    public int MaxRequestNewAddressRetry { get; set; } = 3;
    
    /// <summary>
    /// Maximum retry times for any address operation
    /// Default: 3
    /// </summary>
    public int MaxRequestRetryTimes { get; set; } = 3;
    
    /// <summary>
    /// Addresses that bypass certain restrictions
    /// Used for admin or high-priority addresses
    /// </summary>
    public List<string> WhitelistedAddresses { get; set; } = new();
    
    /// <summary>
    /// Supported coin types for address generation
    /// </summary>
    public List<string> SupportedCoins { get; set; } = new();
    
    /// <summary>
    /// EVM-compatible coins requiring special handling
    /// </summary>
    public List<string> EVMCoins { get; set; } = new();
    
    /// <summary>
    /// Transfer address lists per network/chain
    /// Key: Network identifier, Value: List of transfer addresses
    /// </summary>
    public Dictionary<string, List<string>> TransferAddressLists { get; set; } = new();
}

/// <summary>
/// Payment processing configuration for deposits
/// Manages payment addresses, routing, and processing logic
/// </summary>
public class DepositPaymentConfig
{
    /// <summary>
    /// Payment addresses for different chains and tokens
    /// Key: Chain identifier, Value: Dictionary of token to address mappings
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> PaymentAddresses { get; set; } = new();
    
    /// <summary>
    /// Tokens that should not use swap functionality
    /// Default includes SGR-1 token
    /// </summary>
    public List<string> NoSwapTokens { get; set; } = new() { "SGR-1" };
    
    /// <summary>
    /// Payment processing timeout in seconds
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int PaymentTimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// Whether to enable payment batching for efficiency
    /// Default: true
    /// </summary>
    public bool EnableBatching { get; set; } = true;
    
    /// <summary>
    /// Maximum batch size for payment processing
    /// Default: 100
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;
}

/// <summary>
/// Risk management configuration for deposits
/// Controls fraud detection, limits, and security measures
/// </summary>
public class DepositRiskConfig
{
    /// <summary>
    /// Whether risk management is enabled
    /// Default: true
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Maximum deposit amount per transaction without additional verification
    /// Default: 10000
    /// </summary>
    public decimal MaxDepositWithoutVerification { get; set; } = 10000m;
    
    /// <summary>
    /// Maximum deposits per user per day
    /// Default: 100000
    /// </summary>
    public decimal DailyDepositLimit { get; set; } = 100000m;
    
    /// <summary>
    /// Whether to enable real-time fraud detection
    /// Default: true
    /// </summary>
    public bool EnableFraudDetection { get; set; } = true;
    
    /// <summary>
    /// Risk score threshold for blocking deposits
    /// Range: 0-100, Default: 80
    /// </summary>
    public int RiskScoreThreshold { get; set; } = 80;
    
    /// <summary>
    /// Countries/regions that are blocked from deposits
    /// </summary>
    public List<string> BlockedRegions { get; set; } = new();
    
    /// <summary>
    /// Suspicious address patterns to flag
    /// </summary>
    public List<string> SuspiciousAddressPatterns { get; set; } = new();
}

/// <summary>
/// Notification and alarm configuration for deposits
/// Manages alerts, monitoring, and notification channels
/// </summary>
public class DepositNotificationConfig
{
    /// <summary>
    /// Alarm whitelist per chain and token
    /// Key: Chain identifier, Value: Dictionary of token to address whitelist
    /// </summary>
    public Dictionary<string, Dictionary<string, List<string>>> AlarmWhiteLists { get; set; } = new();
    
    /// <summary>
    /// Whether to send notifications for large deposits
    /// Default: true
    /// </summary>
    public bool NotifyLargeDeposits { get; set; } = true;
    
    /// <summary>
    /// Threshold amount for large deposit notifications
    /// Default: 50000
    /// </summary>
    public decimal LargeDepositThreshold { get; set; } = 50000m;
    
    /// <summary>
    /// Whether to send notifications for failed deposits
    /// Default: true
    /// </summary>
    public bool NotifyFailedDeposits { get; set; } = true;
    
    /// <summary>
    /// Email addresses for deposit notifications
    /// </summary>
    public List<string> NotificationEmails { get; set; } = new();
    
    /// <summary>
    /// Webhook URLs for deposit notifications
    /// </summary>
    public List<string> NotificationWebhooks { get; set; } = new();
    
    /// <summary>
    /// Minimum time between similar notifications (in seconds)
    /// Default: 300 seconds (5 minutes)
    /// </summary>
    public int NotificationCooldownSeconds { get; set; } = 300;
} 