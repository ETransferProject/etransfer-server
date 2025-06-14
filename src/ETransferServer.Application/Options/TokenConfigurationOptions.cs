using System.Collections.Generic;

namespace ETransferServer.Options;

/// <summary>
/// Unified configuration options for all token-related operations
/// Consolidates token settings, access control, and metadata management
/// </summary>
public class TokenConfigurationOptions
{
    /// <summary>
    /// Core token management configuration
    /// </summary>
    public TokenCoreConfig CoreConfig { get; set; } = new();
    
    /// <summary>
    /// Token access and listing configuration
    /// </summary>
    public TokenAccessConfig AccessConfig { get; set; } = new();
    
    /// <summary>
    /// Token operation support configuration
    /// </summary>
    public TokenOperationConfig OperationConfig { get; set; } = new();
    
    /// <summary>
    /// Token swap and exchange configuration
    /// </summary>
    public TokenSwapConfig SwapConfig { get; set; } = new();
    
    /// <summary>
    /// Token security and validation configuration
    /// </summary>
    public TokenSecurityConfig SecurityConfig { get; set; } = new();
    
    /// <summary>
    /// Token metadata and information configuration
    /// </summary>
    public TokenMetadataConfig MetadataConfig { get; set; } = new();
    
    /// <summary>
    /// Token monitoring and notification configuration
    /// </summary>
    public TokenMonitoringConfig MonitoringConfig { get; set; } = new();
}

/// <summary>
/// Core token management configuration
/// Defines basic token properties and management rules
/// </summary>
public class TokenCoreConfig
{
    /// <summary>
    /// Token configurations organized by operation type and chain
    /// Key: Operation type ("Deposit", "Withdraw", "Transfer")
    /// Value: Dictionary of chain to token list mappings
    /// </summary>
    public Dictionary<string, Dictionary<string, List<TokenDefinition>>> TokensByOperation { get; set; } = new();
    
    /// <summary>
    /// Global token registry with comprehensive information
    /// Key: Token symbol, Value: Comprehensive token information
    /// </summary>
    public Dictionary<string, TokenDefinition> GlobalTokenRegistry { get; set; } = new();
    
    /// <summary>
    /// Default token properties applied to new tokens
    /// </summary>
    public TokenDefaultProperties DefaultProperties { get; set; } = new();
    
    /// <summary>
    /// Token validation rules and constraints
    /// </summary>
    public TokenValidationRules ValidationRules { get; set; } = new();
}

/// <summary>
/// Comprehensive token definition
/// </summary>
public class TokenDefinition
{
    /// <summary>
    /// Token display name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Token symbol identifier
    /// </summary>
    public string Symbol { get; set; }
    
    /// <summary>
    /// Number of decimal places
    /// </summary>
    public int Decimals { get; set; }
    
    /// <summary>
    /// Token icon URL
    /// </summary>
    public string Icon { get; set; }
    
    /// <summary>
    /// Contract addresses per chain
    /// Key: Chain identifier, Value: Contract address
    /// </summary>
    public Dictionary<string, string> ContractAddresses { get; set; } = new();
    
    /// <summary>
    /// Token status and availability
    /// </summary>
    public TokenStatus Status { get; set; } = new();
    
    /// <summary>
    /// Token metadata and additional properties
    /// </summary>
    public TokenMetadata Metadata { get; set; } = new();
    
    /// <summary>
    /// Supported operations for this token
    /// </summary>
    public List<string> SupportedOperations { get; set; } = new();
    
    /// <summary>
    /// Chain-specific configurations
    /// Key: Chain identifier, Value: Chain-specific configuration
    /// </summary>
    public Dictionary<string, TokenChainConfig> ChainConfigs { get; set; } = new();
}

/// <summary>
/// Token status information
/// </summary>
public class TokenStatus
{
    /// <summary>
    /// Whether the token is active and available
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Whether the token is listed publicly
    /// </summary>
    public bool IsListed { get; set; } = true;
    
    /// <summary>
    /// Whether the token supports deposits
    /// </summary>
    public bool SupportsDeposit { get; set; } = true;
    
    /// <summary>
    /// Whether the token supports withdrawals
    /// </summary>
    public bool SupportsWithdraw { get; set; } = true;
    
    /// <summary>
    /// Whether the token supports transfers
    /// </summary>
    public bool SupportsTransfer { get; set; } = true;
    
    /// <summary>
    /// Whether the token supports swapping
    /// </summary>
    public bool SupportsSwap { get; set; } = false;
    
    /// <summary>
    /// Current status message
    /// </summary>
    public string StatusMessage { get; set; }
    
    /// <summary>
    /// Last status update timestamp
    /// </summary>
    public long LastUpdated { get; set; }
}

/// <summary>
/// Token metadata information
/// </summary>
public class TokenMetadata
{
    /// <summary>
    /// Token description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Official website URL
    /// </summary>
    public string Website { get; set; }
    
    /// <summary>
    /// White paper URL
    /// </summary>
    public string Whitepaper { get; set; }
    
    /// <summary>
    /// Token category/type
    /// </summary>
    public string Category { get; set; }
    
    /// <summary>
    /// Token tags for classification
    /// </summary>
    public List<string> Tags { get; set; } = new();
    
    /// <summary>
    /// Social media links
    /// </summary>
    public Dictionary<string, string> SocialLinks { get; set; } = new();
    
    /// <summary>
    /// Additional custom properties
    /// </summary>
    public Dictionary<string, string> CustomProperties { get; set; } = new();
}

/// <summary>
/// Chain-specific token configuration
/// </summary>
public class TokenChainConfig
{
    /// <summary>
    /// Contract address on this chain
    /// </summary>
    public string ContractAddress { get; set; }
    
    /// <summary>
    /// Pool address for liquidity operations
    /// </summary>
    public string PoolAddress { get; set; }
    
    /// <summary>
    /// Minimum operation amounts
    /// </summary>
    public TokenAmountLimits AmountLimits { get; set; } = new();
    
    /// <summary>
    /// Fee configuration for this chain
    /// </summary>
    public TokenFeeConfig FeeConfig { get; set; } = new();
    
    /// <summary>
    /// Whether this token is native to this chain
    /// </summary>
    public bool IsNative { get; set; } = false;
    
    /// <summary>
    /// Bridge/cross-chain configuration
    /// </summary>
    public TokenBridgeConfig BridgeConfig { get; set; } = new();
}

/// <summary>
/// Token amount limits and constraints
/// </summary>
public class TokenAmountLimits
{
    /// <summary>
    /// Minimum deposit amount
    /// </summary>
    public decimal MinDeposit { get; set; } = 0;
    
    /// <summary>
    /// Maximum deposit amount
    /// </summary>
    public decimal MaxDeposit { get; set; } = decimal.MaxValue;
    
    /// <summary>
    /// Minimum withdrawal amount
    /// </summary>
    public decimal MinWithdraw { get; set; } = 0;
    
    /// <summary>
    /// Maximum withdrawal amount
    /// </summary>
    public decimal MaxWithdraw { get; set; } = decimal.MaxValue;
    
    /// <summary>
    /// Daily transaction limit
    /// </summary>
    public decimal DailyLimit { get; set; } = decimal.MaxValue;
    
    /// <summary>
    /// Minimum transfer amount
    /// </summary>
    public decimal MinTransfer { get; set; } = 0;
}

/// <summary>
/// Token fee configuration
/// </summary>
public class TokenFeeConfig
{
    /// <summary>
    /// Deposit fee percentage
    /// </summary>
    public decimal DepositFeeRate { get; set; } = 0;
    
    /// <summary>
    /// Withdrawal fee percentage
    /// </summary>
    public decimal WithdrawFeeRate { get; set; } = 0;
    
    /// <summary>
    /// Transfer fee percentage
    /// </summary>
    public decimal TransferFeeRate { get; set; } = 0;
    
    /// <summary>
    /// Fixed fees per operation type
    /// Key: Operation type, Value: Fixed fee amount
    /// </summary>
    public Dictionary<string, decimal> FixedFees { get; set; } = new();
    
    /// <summary>
    /// Gas fee multiplier for this token
    /// </summary>
    public decimal GasFeeMultiplier { get; set; } = 1.0m;
}

/// <summary>
/// Token bridge and cross-chain configuration
/// </summary>
public class TokenBridgeConfig
{
    /// <summary>
    /// Whether this token supports bridging
    /// </summary>
    public bool SupportsBridge { get; set; } = false;
    
    /// <summary>
    /// Supported bridge protocols
    /// </summary>
    public List<string> BridgeProtocols { get; set; } = new();
    
    /// <summary>
    /// Target chains for bridging
    /// </summary>
    public List<string> TargetChains { get; set; } = new();
    
    /// <summary>
    /// Bridge fee configuration
    /// </summary>
    public decimal BridgeFeeRate { get; set; } = 0;
    
    /// <summary>
    /// Minimum bridge amount
    /// </summary>
    public decimal MinBridgeAmount { get; set; } = 0;
}

/// <summary>
/// Default token properties template
/// </summary>
public class TokenDefaultProperties
{
    /// <summary>
    /// Default decimal places for new tokens
    /// </summary>
    public int DefaultDecimals { get; set; } = 18;
    
    /// <summary>
    /// Default icon URL when none specified
    /// </summary>
    public string DefaultIcon { get; set; } = "https://default-icon.example.com/token.png";
    
    /// <summary>
    /// Default minimum amounts
    /// </summary>
    public TokenAmountLimits DefaultLimits { get; set; } = new();
    
    /// <summary>
    /// Default fee configuration
    /// </summary>
    public TokenFeeConfig DefaultFees { get; set; } = new();
    
    /// <summary>
    /// Default supported operations
    /// </summary>
    public List<string> DefaultOperations { get; set; } = new() { "Deposit", "Withdraw", "Transfer" };
}

/// <summary>
/// Token validation rules and constraints
/// </summary>
public class TokenValidationRules
{
    /// <summary>
    /// Required fields for token creation
    /// </summary>
    public List<string> RequiredFields { get; set; } = new() { "Name", "Symbol", "Decimals" };
    
    /// <summary>
    /// Symbol format validation regex
    /// </summary>
    public string SymbolPattern { get; set; } = "^[A-Z][A-Z0-9-]{0,9}$";
    
    /// <summary>
    /// Maximum symbol length
    /// </summary>
    public int MaxSymbolLength { get; set; } = 10;
    
    /// <summary>
    /// Maximum name length
    /// </summary>
    public int MaxNameLength { get; set; } = 50;
    
    /// <summary>
    /// Allowed decimal range
    /// </summary>
    public (int Min, int Max) DecimalRange { get; set; } = (0, 18);
    
    /// <summary>
    /// Blacklisted symbols that cannot be used
    /// </summary>
    public List<string> BlacklistedSymbols { get; set; } = new();
}

/// <summary>
/// Token access and listing configuration
/// </summary>
public class TokenAccessConfig
{
    /// <summary>
    /// Re-application waiting period in hours
    /// </summary>
    public int ReApplyHours { get; set; } = 48;
    
    /// <summary>
    /// Default configuration for new token applications
    /// </summary>
    public TokenAccessDefaults DefaultConfig { get; set; } = new();
    
    /// <summary>
    /// Token-specific access configurations
    /// Key: Token symbol, Value: Access configuration
    /// </summary>
    public Dictionary<string, TokenAccessDefaults> TokenConfigs { get; set; } = new();
    
    /// <summary>
    /// External service configurations for token data
    /// </summary>
    public TokenExternalServices ExternalServices { get; set; } = new();
    
    /// <summary>
    /// Approval workflow configuration
    /// </summary>
    public TokenApprovalConfig ApprovalConfig { get; set; } = new();
}

/// <summary>
/// Token access default settings
/// </summary>
public class TokenAccessDefaults
{
    /// <summary>
    /// Minimum liquidity required (USD)
    /// </summary>
    public decimal MinLiquidityUsd { get; set; } = 1000m;
    
    /// <summary>
    /// Minimum number of token holders
    /// </summary>
    public int MinHolders { get; set; } = 100;
    
    /// <summary>
    /// Minimum market cap (USD)
    /// </summary>
    public decimal MinMarketCapUsd { get; set; } = 10000m;
    
    /// <summary>
    /// Minimum daily trading volume (USD)
    /// </summary>
    public decimal MinDailyVolumeUsd { get; set; } = 1000m;
    
    /// <summary>
    /// Auto-approval thresholds
    /// </summary>
    public TokenAutoApprovalThresholds AutoApproval { get; set; } = new();
}

/// <summary>
/// Auto-approval threshold configuration
/// </summary>
public class TokenAutoApprovalThresholds
{
    /// <summary>
    /// Whether auto-approval is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Liquidity threshold for auto-approval
    /// </summary>
    public decimal LiquidityThreshold { get; set; } = 100000m;
    
    /// <summary>
    /// Market cap threshold for auto-approval
    /// </summary>
    public decimal MarketCapThreshold { get; set; } = 1000000m;
    
    /// <summary>
    /// Holder count threshold for auto-approval
    /// </summary>
    public int HolderThreshold { get; set; } = 1000;
    
    /// <summary>
    /// Volume threshold for auto-approval
    /// </summary>
    public decimal VolumeThreshold { get; set; } = 50000m;
}

/// <summary>
/// External services configuration for token data
/// </summary>
public class TokenExternalServices
{
    /// <summary>
    /// Blockchain scan service configuration
    /// </summary>
    public ExternalServiceConfig ScanService { get; set; } = new();
    
    /// <summary>
    /// Symbol market service configuration
    /// </summary>
    public ExternalServiceConfig SymbolMarketService { get; set; } = new();
    
    /// <summary>
    /// Awaken DEX service configuration
    /// </summary>
    public ExternalServiceConfig AwakenService { get; set; } = new();
    
    /// <summary>
    /// Price feed services
    /// </summary>
    public List<ExternalServiceConfig> PriceFeeds { get; set; } = new();
}

/// <summary>
/// External service endpoint configuration
/// </summary>
public class ExternalServiceConfig
{
    /// <summary>
    /// Service base URL
    /// </summary>
    public string BaseUrl { get; set; }
    
    /// <summary>
    /// API endpoints configuration
    /// Key: Endpoint name, Value: URI path
    /// </summary>
    public Dictionary<string, string> Endpoints { get; set; } = new();
    
    /// <summary>
    /// API key for authentication
    /// </summary>
    public string ApiKey { get; set; }
    
    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Data cache expiration in seconds
    /// </summary>
    public int CacheExpirationSeconds { get; set; } = 180;
    
    /// <summary>
    /// Hash verification key
    /// </summary>
    public string HashVerifyKey { get; set; }
}

/// <summary>
/// Token approval workflow configuration
/// </summary>
public class TokenApprovalConfig
{
    /// <summary>
    /// Whether manual approval is required
    /// </summary>
    public bool RequireManualApproval { get; set; } = true;
    
    /// <summary>
    /// Approval workflow stages
    /// </summary>
    public List<TokenApprovalStage> ApprovalStages { get; set; } = new();
    
    /// <summary>
    /// Auto-rejection criteria
    /// </summary>
    public TokenRejectionCriteria RejectionCriteria { get; set; } = new();
    
    /// <summary>
    /// Notification configuration for approvals
    /// </summary>
    public TokenApprovalNotifications Notifications { get; set; } = new();
}

/// <summary>
/// Token approval stage configuration
/// </summary>
public class TokenApprovalStage
{
    /// <summary>
    /// Stage name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Stage description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Required approvers for this stage
    /// </summary>
    public List<string> RequiredApprovers { get; set; } = new();
    
    /// <summary>
    /// Minimum approval count required
    /// </summary>
    public int MinApprovals { get; set; } = 1;
    
    /// <summary>
    /// Stage timeout in hours
    /// </summary>
    public int TimeoutHours { get; set; } = 72;
    
    /// <summary>
    /// Auto-advance criteria
    /// </summary>
    public Dictionary<string, object> AutoAdvanceCriteria { get; set; } = new();
}

/// <summary>
/// Token rejection criteria configuration
/// </summary>
public class TokenRejectionCriteria
{
    /// <summary>
    /// Blacklisted contract addresses
    /// </summary>
    public List<string> BlacklistedContracts { get; set; } = new();
    
    /// <summary>
    /// Blacklisted token symbols
    /// </summary>
    public List<string> BlacklistedSymbols { get; set; } = new();
    
    /// <summary>
    /// Suspicious patterns in token names
    /// </summary>
    public List<string> SuspiciousNamePatterns { get; set; } = new();
    
    /// <summary>
    /// Minimum requirements for approval
    /// </summary>
    public TokenAccessDefaults MinimumRequirements { get; set; } = new();
}

/// <summary>
/// Token approval notifications configuration
/// </summary>
public class TokenApprovalNotifications
{
    /// <summary>
    /// Email addresses for approval notifications
    /// </summary>
    public List<string> NotificationEmails { get; set; } = new();
    
    /// <summary>
    /// Webhook URLs for approval events
    /// </summary>
    public List<string> WebhookUrls { get; set; } = new();
    
    /// <summary>
    /// Slack notification configuration
    /// </summary>
    public TokenSlackConfig SlackConfig { get; set; } = new();
}

/// <summary>
/// Slack notification configuration for tokens
/// </summary>
public class TokenSlackConfig
{
    /// <summary>
    /// Whether Slack notifications are enabled
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Slack webhook URL
    /// </summary>
    public string WebhookUrl { get; set; }
    
    /// <summary>
    /// Default channel for notifications
    /// </summary>
    public string DefaultChannel { get; set; } = "#token-approvals";
    
    /// <summary>
    /// Bot username
    /// </summary>
    public string BotUsername { get; set; } = "ETransfer-Token-Bot";
}

/// <summary>
/// Token operation support configuration
/// Controls which operations are supported for tokens across different chains
/// </summary>
public class TokenOperationConfig
{
    /// <summary>
    /// Supported chains for each operation type
    /// Key: Token symbol, Value: Operation support mapping
    /// </summary>
    public Dictionary<string, TokenOperationSupport> TokenOperations { get; set; } = new();
    
    /// <summary>
    /// Global operation settings
    /// </summary>
    public GlobalOperationSettings GlobalSettings { get; set; } = new();
    
    /// <summary>
    /// Operation-specific configurations
    /// </summary>
    public OperationSpecificConfigs OperationConfigs { get; set; } = new();
}

/// <summary>
/// Token operation support mapping
/// </summary>
public class TokenOperationSupport
{
    /// <summary>
    /// Chains supporting deposit for this token
    /// </summary>
    public List<string> DepositChains { get; set; } = new();
    
    /// <summary>
    /// Chains supporting withdrawal for this token
    /// </summary>
    public List<string> WithdrawChains { get; set; } = new();
    
    /// <summary>
    /// Chains supporting transfer for this token
    /// </summary>
    public List<string> TransferChains { get; set; } = new();
    
    /// <summary>
    /// Chains supporting swap for this token
    /// </summary>
    public List<string> SwapChains { get; set; } = new();
    
    /// <summary>
    /// Cross-chain operation support
    /// </summary>
    public CrossChainOperationSupport CrossChain { get; set; } = new();
}

/// <summary>
/// Cross-chain operation support configuration
/// </summary>
public class CrossChainOperationSupport
{
    /// <summary>
    /// Supported bridge pairs
    /// Key: Source chain, Value: List of target chains
    /// </summary>
    public Dictionary<string, List<string>> BridgePairs { get; set; } = new();
    
    /// <summary>
    /// Cross-chain transfer routes
    /// </summary>
    public List<CrossChainRoute> TransferRoutes { get; set; } = new();
    
    /// <summary>
    /// Bridge fee configuration
    /// </summary>
    public CrossChainFeeConfig FeeConfig { get; set; } = new();
}

/// <summary>
/// Cross-chain transfer route definition
/// </summary>
public class CrossChainRoute
{
    /// <summary>
    /// Route identifier
    /// </summary>
    public string RouteId { get; set; }
    
    /// <summary>
    /// Source chain
    /// </summary>
    public string FromChain { get; set; }
    
    /// <summary>
    /// Destination chain
    /// </summary>
    public string ToChain { get; set; }
    
    /// <summary>
    /// Intermediate steps
    /// </summary>
    public List<string> IntermediateSteps { get; set; } = new();
    
    /// <summary>
    /// Estimated processing time in seconds
    /// </summary>
    public int EstimatedTimeSeconds { get; set; }
    
    /// <summary>
    /// Route status
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Cross-chain fee configuration
/// </summary>
public class CrossChainFeeConfig
{
    /// <summary>
    /// Base fee for cross-chain operations
    /// </summary>
    public decimal BaseFee { get; set; } = 0.001m;
    
    /// <summary>
    /// Fee percentage of transfer amount
    /// </summary>
    public decimal FeePercentage { get; set; } = 0.001m;
    
    /// <summary>
    /// Minimum cross-chain fee
    /// </summary>
    public decimal MinFee { get; set; } = 0.0001m;
    
    /// <summary>
    /// Maximum cross-chain fee
    /// </summary>
    public decimal MaxFee { get; set; } = 100m;
    
    /// <summary>
    /// Chain-specific fee multipliers
    /// Key: Chain identifier, Value: Fee multiplier
    /// </summary>
    public Dictionary<string, decimal> ChainFeeMultipliers { get; set; } = new();
}

/// <summary>
/// Global operation settings
/// </summary>
public class GlobalOperationSettings
{
    /// <summary>
    /// Whether new tokens are enabled by default
    /// </summary>
    public bool EnableNewTokensByDefault { get; set; } = false;
    
    /// <summary>
    /// Default operation timeout in seconds
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// Maximum concurrent operations per token
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 100;
    
    /// <summary>
    /// Operation rate limiting
    /// </summary>
    public OperationRateLimiting RateLimiting { get; set; } = new();
}

/// <summary>
/// Operation rate limiting configuration
/// </summary>
public class OperationRateLimiting
{
    /// <summary>
    /// Maximum operations per minute per token
    /// </summary>
    public int MaxOperationsPerMinute { get; set; } = 60;
    
    /// <summary>
    /// Maximum operations per hour per user
    /// </summary>
    public int MaxOperationsPerHourPerUser { get; set; } = 1000;
    
    /// <summary>
    /// Burst limit for short-term spikes
    /// </summary>
    public int BurstLimit { get; set; } = 10;
    
    /// <summary>
    /// Rate limit reset period in minutes
    /// </summary>
    public int ResetPeriodMinutes { get; set; } = 1;
}

/// <summary>
/// Operation-specific configurations
/// </summary>
public class OperationSpecificConfigs
{
    /// <summary>
    /// Deposit operation configuration
    /// </summary>
    public DepositOperationConfig DepositConfig { get; set; } = new();
    
    /// <summary>
    /// Withdrawal operation configuration
    /// </summary>
    public WithdrawOperationConfig WithdrawConfig { get; set; } = new();
    
    /// <summary>
    /// Transfer operation configuration
    /// </summary>
    public TransferOperationConfig TransferConfig { get; set; } = new();
}

/// <summary>
/// Deposit operation specific configuration
/// </summary>
public class DepositOperationConfig
{
    /// <summary>
    /// Minimum confirmation blocks required
    /// </summary>
    public int MinConfirmations { get; set; } = 12;
    
    /// <summary>
    /// Auto-credit threshold
    /// </summary>
    public decimal AutoCreditThreshold { get; set; } = 1000m;
    
    /// <summary>
    /// Deposit timeout in minutes
    /// </summary>
    public int TimeoutMinutes { get; set; } = 60;
    
    /// <summary>
    /// Whether to enable fast deposits
    /// </summary>
    public bool EnableFastDeposits { get; set; } = true;
}

/// <summary>
/// Withdrawal operation specific configuration
/// </summary>
public class WithdrawOperationConfig
{
    /// <summary>
    /// Whether to require email confirmation
    /// </summary>
    public bool RequireEmailConfirmation { get; set; } = true;
    
    /// <summary>
    /// Whether to require 2FA
    /// </summary>
    public bool Require2FA { get; set; } = true;
    
    /// <summary>
    /// Withdrawal processing delay in minutes
    /// </summary>
    public int ProcessingDelayMinutes { get; set; } = 5;
    
    /// <summary>
    /// Manual approval threshold
    /// </summary>
    public decimal ManualApprovalThreshold { get; set; } = 10000m;
}

/// <summary>
/// Transfer operation specific configuration
/// </summary>
public class TransferOperationConfig
{
    /// <summary>
    /// Whether to enable instant transfers
    /// </summary>
    public bool EnableInstantTransfers { get; set; } = true;
    
    /// <summary>
    /// Maximum transfer amount per transaction
    /// </summary>
    public decimal MaxTransferAmount { get; set; } = 1000000m;
    
    /// <summary>
    /// Daily transfer limit per user
    /// </summary>
    public decimal DailyTransferLimit { get; set; } = 100000m;
    
    /// <summary>
    /// Whether to require verification for large transfers
    /// </summary>
    public bool RequireVerificationForLargeTransfers { get; set; } = true;
}

/// <summary>
/// Token swap and exchange configuration
/// Manages token swap operations and routing
/// </summary>
public class TokenSwapConfig
{
    /// <summary>
    /// Available swap pairs
    /// </summary>
    public List<TokenSwapPair> SwapPairs { get; set; } = new();
    
    /// <summary>
    /// Swap routing configuration
    /// </summary>
    public SwapRoutingConfig RoutingConfig { get; set; } = new();
    
    /// <summary>
    /// Swap fee configuration
    /// </summary>
    public SwapFeeConfig FeeConfig { get; set; } = new();
    
    /// <summary>
    /// Liquidity pool configuration
    /// </summary>
    public LiquidityPoolConfig PoolConfig { get; set; } = new();
}

/// <summary>
/// Token swap pair definition
/// </summary>
public class TokenSwapPair
{
    /// <summary>
    /// Source token symbol
    /// </summary>
    public string FromToken { get; set; }
    
    /// <summary>
    /// Target token symbol
    /// </summary>
    public string ToToken { get; set; }
    
    /// <summary>
    /// Supported target chains for the destination token
    /// </summary>
    public List<string> ToChains { get; set; } = new();
    
    /// <summary>
    /// Swap pair status
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Minimum swap amount
    /// </summary>
    public decimal MinSwapAmount { get; set; } = 0.001m;
    
    /// <summary>
    /// Maximum swap amount
    /// </summary>
    public decimal MaxSwapAmount { get; set; } = 1000000m;
    
    /// <summary>
    /// Swap fee rate
    /// </summary>
    public decimal FeeRate { get; set; } = 0.003m;
    
    /// <summary>
    /// Price slippage tolerance
    /// </summary>
    public decimal SlippageTolerance { get; set; } = 0.05m;
}

/// <summary>
/// Swap routing configuration
/// </summary>
public class SwapRoutingConfig
{
    /// <summary>
    /// Maximum number of hops for swap routing
    /// </summary>
    public int MaxHops { get; set; } = 3;
    
    /// <summary>
    /// Preferred routing protocols
    /// </summary>
    public List<string> PreferredProtocols { get; set; } = new();
    
    /// <summary>
    /// Route optimization strategy
    /// </summary>
    public string OptimizationStrategy { get; set; } = "BestPrice";
    
    /// <summary>
    /// Route caching configuration
    /// </summary>
    public RouteCacheConfig CacheConfig { get; set; } = new();
}

/// <summary>
/// Route caching configuration
/// </summary>
public class RouteCacheConfig
{
    /// <summary>
    /// Whether to enable route caching
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Cache expiration time in seconds
    /// </summary>
    public int ExpirationSeconds { get; set; } = 300;
    
    /// <summary>
    /// Maximum cached routes per pair
    /// </summary>
    public int MaxCachedRoutes { get; set; } = 10;
}

/// <summary>
/// Swap fee configuration
/// </summary>
public class SwapFeeConfig
{
    /// <summary>
    /// Base swap fee rate
    /// </summary>
    public decimal BaseFeeRate { get; set; } = 0.003m;
    
    /// <summary>
    /// Volume-based fee discounts
    /// Key: Volume threshold, Value: Discount rate
    /// </summary>
    public Dictionary<decimal, decimal> VolumeDiscounts { get; set; } = new();
    
    /// <summary>
    /// Partner fee sharing configuration
    /// </summary>
    public PartnerFeeConfig PartnerFees { get; set; } = new();
}

/// <summary>
/// Partner fee sharing configuration
/// </summary>
public class PartnerFeeConfig
{
    /// <summary>
    /// Whether partner fee sharing is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Partner fee share percentage
    /// </summary>
    public decimal PartnerShareRate { get; set; } = 0.5m;
    
    /// <summary>
    /// Minimum fee amount for sharing
    /// </summary>
    public decimal MinFeeForSharing { get; set; } = 0.001m;
}

/// <summary>
/// Liquidity pool configuration
/// </summary>
public class LiquidityPoolConfig
{
    /// <summary>
    /// Default pool configurations
    /// </summary>
    public PoolDefaults DefaultConfig { get; set; } = new();
    
    /// <summary>
    /// Token-specific pool configurations
    /// Key: Token symbol, Value: Pool configuration
    /// </summary>
    public Dictionary<string, PoolDefaults> TokenPoolConfigs { get; set; } = new();
    
    /// <summary>
    /// Pool management settings
    /// </summary>
    public PoolManagementSettings ManagementSettings { get; set; } = new();
}

/// <summary>
/// Pool default configuration
/// </summary>
public class PoolDefaults
{
    /// <summary>
    /// Minimum liquidity required (USD)
    /// </summary>
    public decimal MinLiquidityUsd { get; set; } = 1000m;
    
    /// <summary>
    /// Target liquidity (USD)
    /// </summary>
    public decimal TargetLiquidityUsd { get; set; } = 10000m;
    
    /// <summary>
    /// Liquidity provider fee rate
    /// </summary>
    public decimal LpFeeRate { get; set; } = 0.0025m;
    
    /// <summary>
    /// Pool rebalancing threshold
    /// </summary>
    public decimal RebalanceThreshold { get; set; } = 0.1m;
}

/// <summary>
/// Pool management settings
/// </summary>
public class PoolManagementSettings
{
    /// <summary>
    /// Auto-rebalancing enabled
    /// </summary>
    public bool AutoRebalancing { get; set; } = true;
    
    /// <summary>
    /// Rebalancing interval in minutes
    /// </summary>
    public int RebalanceIntervalMinutes { get; set; } = 60;
    
    /// <summary>
    /// Emergency pause threshold
    /// </summary>
    public decimal EmergencyPauseThreshold { get; set; } = 0.5m;
    
    /// <summary>
    /// Pool monitoring alerts
    /// </summary>
    public bool EnableMonitoringAlerts { get; set; } = true;
}

/// <summary>
/// Token security and validation configuration
/// Manages security policies and validation rules for tokens
/// </summary>
public class TokenSecurityConfig
{
    /// <summary>
    /// Contract security validation
    /// </summary>
    public ContractSecurityConfig ContractSecurity { get; set; } = new();
    
    /// <summary>
    /// Anti-fraud and security measures
    /// </summary>
    public AntiFraudConfig AntiFraud { get; set; } = new();
    
    /// <summary>
    /// Compliance and regulatory settings
    /// </summary>
    public ComplianceConfig Compliance { get; set; } = new();
    
    /// <summary>
    /// Security monitoring configuration
    /// </summary>
    public SecurityMonitoringConfig Monitoring { get; set; } = new();
}

/// <summary>
/// Contract security validation configuration
/// </summary>
public class ContractSecurityConfig
{
    /// <summary>
    /// Whether to perform contract verification
    /// </summary>
    public bool EnableContractVerification { get; set; } = true;
    
    /// <summary>
    /// Trusted contract deployers
    /// </summary>
    public List<string> TrustedDeployers { get; set; } = new();
    
    /// <summary>
    /// Blacklisted contract addresses
    /// </summary>
    public List<string> BlacklistedContracts { get; set; } = new();
    
    /// <summary>
    /// Required contract features
    /// </summary>
    public List<string> RequiredFeatures { get; set; } = new();
    
    /// <summary>
    /// Security score thresholds
    /// </summary>
    public SecurityScoreThresholds ScoreThresholds { get; set; } = new();
}

/// <summary>
/// Security score thresholds
/// </summary>
public class SecurityScoreThresholds
{
    /// <summary>
    /// Minimum security score for approval
    /// </summary>
    public int MinSecurityScore { get; set; } = 70;
    
    /// <summary>
    /// Score for automatic approval
    /// </summary>
    public int AutoApprovalScore { get; set; } = 90;
    
    /// <summary>
    /// Score for automatic rejection
    /// </summary>
    public int AutoRejectionScore { get; set; } = 30;
}

/// <summary>
/// Anti-fraud configuration
/// </summary>
public class AntiFraudConfig
{
    /// <summary>
    /// Whether fraud detection is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Suspicious pattern detection
    /// </summary>
    public List<string> SuspiciousPatterns { get; set; } = new();
    
    /// <summary>
    /// Risk score calculation weights
    /// </summary>
    public RiskScoreWeights RiskWeights { get; set; } = new();
    
    /// <summary>
    /// Fraud prevention measures
    /// </summary>
    public FraudPreventionMeasures PreventionMeasures { get; set; } = new();
}

/// <summary>
/// Risk score calculation weights
/// </summary>
public class RiskScoreWeights
{
    /// <summary>
    /// Contract age weight
    /// </summary>
    public decimal ContractAgeWeight { get; set; } = 0.2m;
    
    /// <summary>
    /// Trading volume weight
    /// </summary>
    public decimal VolumeWeight { get; set; } = 0.3m;
    
    /// <summary>
    /// Holder count weight
    /// </summary>
    public decimal HolderCountWeight { get; set; } = 0.2m;
    
    /// <summary>
    /// Liquidity weight
    /// </summary>
    public decimal LiquidityWeight { get; set; } = 0.3m;
}

/// <summary>
/// Fraud prevention measures
/// </summary>
public class FraudPreventionMeasures
{
    /// <summary>
    /// Honeypot detection enabled
    /// </summary>
    public bool HoneypotDetection { get; set; } = true;
    
    /// <summary>
    /// Rug pull detection enabled
    /// </summary>
    public bool RugPullDetection { get; set; } = true;
    
    /// <summary>
    /// Flash loan attack detection
    /// </summary>
    public bool FlashLoanAttackDetection { get; set; } = true;
    
    /// <summary>
    /// Pump and dump detection
    /// </summary>
    public bool PumpAndDumpDetection { get; set; } = true;
}

/// <summary>
/// Compliance and regulatory configuration
/// </summary>
public class ComplianceConfig
{
    /// <summary>
    /// KYC requirements for token operations
    /// </summary>
    public KycRequirements KycRequirements { get; set; } = new();
    
    /// <summary>
    /// AML monitoring settings
    /// </summary>
    public AmlMonitoringConfig AmlMonitoring { get; set; } = new();
    
    /// <summary>
    /// Regulatory reporting configuration
    /// </summary>
    public RegulatoryReportingConfig Reporting { get; set; } = new();
}

/// <summary>
/// KYC requirements configuration
/// </summary>
public class KycRequirements
{
    /// <summary>
    /// KYC required for deposits above threshold
    /// </summary>
    public decimal DepositKycThreshold { get; set; } = 10000m;
    
    /// <summary>
    /// KYC required for withdrawals above threshold
    /// </summary>
    public decimal WithdrawKycThreshold { get; set; } = 5000m;
    
    /// <summary>
    /// Enhanced KYC threshold
    /// </summary>
    public decimal EnhancedKycThreshold { get; set; } = 50000m;
    
    /// <summary>
    /// Restricted jurisdictions
    /// </summary>
    public List<string> RestrictedJurisdictions { get; set; } = new();
}

/// <summary>
/// AML monitoring configuration
/// </summary>
public class AmlMonitoringConfig
{
    /// <summary>
    /// Whether AML monitoring is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Suspicious activity thresholds
    /// </summary>
    public SuspiciousActivityThresholds Thresholds { get; set; } = new();
    
    /// <summary>
    /// Monitoring rules
    /// </summary>
    public List<AmlRule> MonitoringRules { get; set; } = new();
}

/// <summary>
/// Suspicious activity thresholds
/// </summary>
public class SuspiciousActivityThresholds
{
    /// <summary>
    /// Large transaction threshold
    /// </summary>
    public decimal LargeTransactionThreshold { get; set; } = 100000m;
    
    /// <summary>
    /// Frequent transaction count threshold
    /// </summary>
    public int FrequentTransactionThreshold { get; set; } = 50;
    
    /// <summary>
    /// Velocity threshold (amount per hour)
    /// </summary>
    public decimal VelocityThreshold { get; set; } = 500000m;
}

/// <summary>
/// AML monitoring rule
/// </summary>
public class AmlRule
{
    /// <summary>
    /// Rule identifier
    /// </summary>
    public string RuleId { get; set; }
    
    /// <summary>
    /// Rule description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Rule conditions
    /// </summary>
    public Dictionary<string, object> Conditions { get; set; } = new();
    
    /// <summary>
    /// Actions to take when rule is triggered
    /// </summary>
    public List<string> Actions { get; set; } = new();
    
    /// <summary>
    /// Rule priority
    /// </summary>
    public int Priority { get; set; } = 1;
}

/// <summary>
/// Regulatory reporting configuration
/// </summary>
public class RegulatoryReportingConfig
{
    /// <summary>
    /// Whether reporting is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Reporting frequency
    /// </summary>
    public string ReportingFrequency { get; set; } = "Daily";
    
    /// <summary>
    /// Report recipients
    /// </summary>
    public List<string> ReportRecipients { get; set; } = new();
    
    /// <summary>
    /// Report formats
    /// </summary>
    public List<string> ReportFormats { get; set; } = new() { "JSON", "CSV" };
}

/// <summary>
/// Security monitoring configuration
/// </summary>
public class SecurityMonitoringConfig
{
    /// <summary>
    /// Whether real-time monitoring is enabled
    /// </summary>
    public bool RealTimeMonitoring { get; set; } = true;
    
    /// <summary>
    /// Alert thresholds
    /// </summary>
    public SecurityAlertThresholds AlertThresholds { get; set; } = new();
    
    /// <summary>
    /// Incident response configuration
    /// </summary>
    public IncidentResponseConfig IncidentResponse { get; set; } = new();
}

/// <summary>
/// Security alert thresholds
/// </summary>
public class SecurityAlertThresholds
{
    /// <summary>
    /// Failed transaction threshold
    /// </summary>
    public int FailedTransactionThreshold { get; set; } = 10;
    
    /// <summary>
    /// Unusual activity threshold
    /// </summary>
    public decimal UnusualActivityThreshold { get; set; } = 1000000m;
    
    /// <summary>
    /// Security score drop threshold
    /// </summary>
    public int SecurityScoreDropThreshold { get; set; } = 20;
}

/// <summary>
/// Incident response configuration
/// </summary>
public class IncidentResponseConfig
{
    /// <summary>
    /// Auto-pause suspicious tokens
    /// </summary>
    public bool AutoPauseSuspiciousTokens { get; set; } = true;
    
    /// <summary>
    /// Emergency response team contacts
    /// </summary>
    public List<string> EmergencyContacts { get; set; } = new();
    
    /// <summary>
    /// Incident escalation thresholds
    /// </summary>
    public Dictionary<string, int> EscalationThresholds { get; set; } = new();
}

/// <summary>
/// Token metadata and information configuration
/// Manages token metadata, descriptions, and external information sources
/// </summary>
public class TokenMetadataConfig
{
    /// <summary>
    /// Metadata source configuration
    /// </summary>
    public MetadataSourceConfig SourceConfig { get; set; } = new();
    
    /// <summary>
    /// Metadata validation rules
    /// </summary>
    public MetadataValidationRules ValidationRules { get; set; } = new();
    
    /// <summary>
    /// Metadata caching configuration
    /// </summary>
    public MetadataCacheConfig CacheConfig { get; set; } = new();
    
    /// <summary>
    /// Metadata enrichment configuration
    /// </summary>
    public MetadataEnrichmentConfig EnrichmentConfig { get; set; } = new();
}

/// <summary>
/// Metadata source configuration
/// </summary>
public class MetadataSourceConfig
{
    /// <summary>
    /// Primary metadata sources
    /// </summary>
    public List<MetadataSource> PrimarySources { get; set; } = new();
    
    /// <summary>
    /// Fallback metadata sources
    /// </summary>
    public List<MetadataSource> FallbackSources { get; set; } = new();
    
    /// <summary>
    /// Source priority configuration
    /// </summary>
    public Dictionary<string, int> SourcePriorities { get; set; } = new();
}

/// <summary>
/// Metadata source definition
/// </summary>
public class MetadataSource
{
    /// <summary>
    /// Source identifier
    /// </summary>
    public string SourceId { get; set; }
    
    /// <summary>
    /// Source name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Source type (API, Database, File, etc.)
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Source endpoint or connection string
    /// </summary>
    public string Endpoint { get; set; }
    
    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Metadata validation rules
/// </summary>
public class MetadataValidationRules
{
    /// <summary>
    /// Required metadata fields
    /// </summary>
    public List<string> RequiredFields { get; set; } = new() { "Name", "Symbol", "Icon" };
    
    /// <summary>
    /// Field format validators
    /// Key: Field name, Value: Regex pattern
    /// </summary>
    public Dictionary<string, string> FormatValidators { get; set; } = new();
    
    /// <summary>
    /// Field length constraints
    /// Key: Field name, Value: (MinLength, MaxLength)
    /// </summary>
    public Dictionary<string, (int Min, int Max)> LengthConstraints { get; set; } = new();
}

/// <summary>
/// Metadata caching configuration
/// </summary>
public class MetadataCacheConfig
{
    /// <summary>
    /// Whether caching is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Default cache expiration in seconds
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 3600;
    
    /// <summary>
    /// Maximum cache entries
    /// </summary>
    public int MaxEntries { get; set; } = 10000;
}

/// <summary>
/// Metadata enrichment configuration
/// </summary>
public class MetadataEnrichmentConfig
{
    /// <summary>
    /// Whether auto-enrichment is enabled
    /// </summary>
    public bool AutoEnrichment { get; set; } = true;
    
    /// <summary>
    /// Batch processing enabled
    /// </summary>
    public bool BatchProcessing { get; set; } = true;
    
    /// <summary>
    /// Batch size
    /// </summary>
    public int BatchSize { get; set; } = 100;
    
    /// <summary>
    /// Processing interval in minutes
    /// </summary>
    public int ProcessingIntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Token monitoring and notification configuration
/// Manages real-time monitoring, alerts, and notifications for token operations
/// </summary>
public class TokenMonitoringConfig
{
    /// <summary>
    /// Real-time monitoring configuration
    /// </summary>
    public RealTimeMonitoringConfig RealTimeMonitoring { get; set; } = new();
    
    /// <summary>
    /// Alert configuration
    /// </summary>
    public TokenAlertConfig AlertConfig { get; set; } = new();
    
    /// <summary>
    /// Notification configuration
    /// </summary>
    public TokenNotificationConfig NotificationConfig { get; set; } = new();
    
    /// <summary>
    /// Performance monitoring configuration
    /// </summary>
    public PerformanceMonitoringConfig PerformanceMonitoring { get; set; } = new();
    
    /// <summary>
    /// Audit and logging configuration
    /// </summary>
    public AuditLoggingConfig AuditLogging { get; set; } = new();
}

/// <summary>
/// Real-time monitoring configuration
/// </summary>
public class RealTimeMonitoringConfig
{
    /// <summary>
    /// Whether real-time monitoring is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Monitoring intervals for different metrics
    /// Key: Metric type, Value: Interval in seconds
    /// </summary>
    public Dictionary<string, int> MonitoringIntervals { get; set; } = new();
    
    /// <summary>
    /// Raw data retention period in days
    /// </summary>
    public int RawDataRetentionDays { get; set; } = 7;
    
    /// <summary>
    /// Aggregated data retention period in days
    /// </summary>
    public int AggregatedDataRetentionDays { get; set; } = 90;
}

/// <summary>
/// Token alert configuration
/// </summary>
public class TokenAlertConfig
{
    /// <summary>
    /// Whether alerts are enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Alert rules
    /// </summary>
    public List<TokenAlertRule> AlertRules { get; set; } = new();
    
    /// <summary>
    /// Maximum alerts per minute
    /// </summary>
    public int MaxAlertsPerMinute { get; set; } = 10;
    
    /// <summary>
    /// Suppression period for duplicate alerts in seconds
    /// </summary>
    public int DuplicateSuppressionSeconds { get; set; } = 300;
}

/// <summary>
/// Token alert rule
/// </summary>
public class TokenAlertRule
{
    /// <summary>
    /// Rule identifier
    /// </summary>
    public string RuleId { get; set; }
    
    /// <summary>
    /// Rule name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Rule description
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Alert severity
    /// </summary>
    public string Severity { get; set; } = "Medium";
    
    /// <summary>
    /// Alert conditions
    /// </summary>
    public List<AlertCondition> Conditions { get; set; } = new();
    
    /// <summary>
    /// Applicable tokens (patterns)
    /// </summary>
    public List<string> TokenPatterns { get; set; } = new();
    
    /// <summary>
    /// Alert actions
    /// </summary>
    public List<AlertAction> Actions { get; set; } = new();
    
    /// <summary>
    /// Rule status
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Alert condition
/// </summary>
public class AlertCondition
{
    /// <summary>
    /// Condition type
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Metric to evaluate
    /// </summary>
    public string Metric { get; set; }
    
    /// <summary>
    /// Comparison operator
    /// </summary>
    public string Operator { get; set; }
    
    /// <summary>
    /// Threshold value
    /// </summary>
    public decimal Threshold { get; set; }
    
    /// <summary>
    /// Evaluation window in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 300;
}

/// <summary>
/// Alert action
/// </summary>
public class AlertAction
{
    /// <summary>
    /// Action type
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Action configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// Action delay in seconds
    /// </summary>
    public int DelaySeconds { get; set; } = 0;
}

/// <summary>
/// Token notification configuration
/// </summary>
public class TokenNotificationConfig
{
    /// <summary>
    /// Whether notifications are enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Notification channels
    /// </summary>
    public List<NotificationChannel> Channels { get; set; } = new();
    
    /// <summary>
    /// Notification templates
    /// </summary>
    public Dictionary<string, NotificationTemplate> Templates { get; set; } = new();
}

/// <summary>
/// Notification channel configuration
/// </summary>
public class NotificationChannel
{
    /// <summary>
    /// Channel identifier
    /// </summary>
    public string ChannelId { get; set; }
    
    /// <summary>
    /// Channel name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Channel type (Email, Slack, Webhook, etc.)
    /// </summary>
    public string Type { get; set; }
    
    /// <summary>
    /// Channel configuration
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
    
    /// <summary>
    /// Channel status
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Notification template
/// </summary>
public class NotificationTemplate
{
    /// <summary>
    /// Template identifier
    /// </summary>
    public string TemplateId { get; set; }
    
    /// <summary>
    /// Template name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Template subject
    /// </summary>
    public string Subject { get; set; }
    
    /// <summary>
    /// Template body
    /// </summary>
    public string Body { get; set; }
    
    /// <summary>
    /// Template format (HTML, Text, Markdown)
    /// </summary>
    public string Format { get; set; } = "HTML";
}

/// <summary>
/// Performance monitoring configuration
/// </summary>
public class PerformanceMonitoringConfig
{
    /// <summary>
    /// Whether performance monitoring is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Performance metrics to track
    /// </summary>
    public List<PerformanceMetric> Metrics { get; set; } = new();
    
    /// <summary>
    /// Performance benchmarks
    /// </summary>
    public Dictionary<string, PerformanceBenchmark> Benchmarks { get; set; } = new();
}

/// <summary>
/// Performance metric definition
/// </summary>
public class PerformanceMetric
{
    /// <summary>
    /// Metric identifier
    /// </summary>
    public string MetricId { get; set; }
    
    /// <summary>
    /// Metric name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Metric unit
    /// </summary>
    public string Unit { get; set; }
    
    /// <summary>
    /// Collection frequency in seconds
    /// </summary>
    public int CollectionFrequencySeconds { get; set; } = 60;
}

/// <summary>
/// Performance benchmark
/// </summary>
public class PerformanceBenchmark
{
    /// <summary>
    /// Benchmark name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Target value
    /// </summary>
    public decimal TargetValue { get; set; }
    
    /// <summary>
    /// Acceptable range
    /// </summary>
    public (decimal Min, decimal Max) AcceptableRange { get; set; }
}

/// <summary>
/// Audit and logging configuration
/// </summary>
public class AuditLoggingConfig
{
    /// <summary>
    /// Whether audit logging is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Audit log level
    /// </summary>
    public string LogLevel { get; set; } = "Information";
    
    /// <summary>
    /// Events to audit
    /// </summary>
    public List<string> AuditEvents { get; set; } = new();
    
    /// <summary>
    /// Retention period in days
    /// </summary>
    public int RetentionDays { get; set; } = 90;
    
    /// <summary>
    /// Whether to archive old logs
    /// </summary>
    public bool ArchiveOldLogs { get; set; } = true;
}

#region Backward Compatibility Classes

/// <summary>
/// Backward compatibility class for existing TokenOptions usage
/// </summary>
public class TokenOptions
{
    public Dictionary<string, List<TokenConfig>> Deposit { get; set; } = new();
    public Dictionary<string, List<TokenConfig>> Withdraw { get; set; } = new();
    public List<TokenConfig> Transfer { get; set; } = new();
    public List<TokenSwapConfigLegacy> DepositSwap { get; set; } = new();
}

/// <summary>
/// Backward compatibility class for existing TokenConfig usage
/// </summary>
public class TokenConfig
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string Icon { get; set; }
    public string ContractAddress { get; set; }
}

/// <summary>
/// Backward compatibility class for existing TokenSwapConfig usage (renamed to avoid conflict)
/// </summary>
public class TokenSwapConfigLegacy
{
    public string Name { get; set; }
    public string Symbol { get; set; }
    public int Decimals { get; set; }
    public string Icon { get; set; }
    public string ContractAddress { get; set; }
    public List<ToTokenConfig> ToTokenList { get; set; } = new();
}

/// <summary>
/// Backward compatibility class for existing ToTokenConfig usage
/// </summary>
public class ToTokenConfig
{
    public string Symbol { get; set; }
    public string Name { get; set; }
    public List<string> ChainIdList { get; set; } = new();
    public string Icon { get; set; }
}

/// <summary>
/// Backward compatibility class for existing ToTokenInfo usage
/// </summary>
public class ToTokenInfo
{
    public string Symbol { get; set; }
    public List<string> ChainIdList { get; set; } = new();
}

#endregion 