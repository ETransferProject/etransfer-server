using System.Collections.Generic;

namespace ETransferServer.Options;

/// <summary>
/// Configuration options for network and blockchain operations
/// Defines supported networks, fee structures, and operational parameters
/// </summary>
public class NetworkOptions
{
    /// <summary>
    /// Network pattern mappings for address validation and routing
    /// Key: Network identifier, Value: List of pattern strings for address recognition
    /// </summary>
    public Dictionary<string, List<string>> NetworkPattern { get; set; } = new();
    
    /// <summary>
    /// Networks that support withdrawal fees
    /// List of network identifiers where withdrawal fees are applicable
    /// </summary>
    public List<string> WithdrawFeeNetwork { get; set; } = new();
    
    /// <summary>
    /// Complete network configuration mapping
    /// Key: Token symbol, Value: List of network configurations for that token
    /// </summary>
    public Dictionary<string, List<NetworkConfig>> NetworkMap { get; set; } = new();
    
    /// <summary>
    /// Global 24-hour withdrawal limit in base currency units
    /// Default: 1,000,000 units
    /// </summary>
    public decimal WithdrawLimit24H { get; set; } = 10_0000;
}

/// <summary>
/// Complete configuration for a specific network supporting a token
/// Contains network information, deposit settings, and withdrawal settings
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// Basic network information and operational parameters
    /// </summary>
    public NetworkInfo NetworkInfo { get; set; }
    
    /// <summary>
    /// Deposit-specific configuration for this network
    /// </summary>
    public DepositInfo DepositInfo { get; set; }
    
    /// <summary>
    /// Withdrawal-specific configuration for this network
    /// </summary>
    public WithdrawInfo WithdrawInfo { get; set; }
    
    /// <summary>
    /// List of supported operation types on this network
    /// e.g., ["deposit", "withdraw", "transfer"]
    /// </summary>
    public List<string> SupportType { get; set; } = new();
    
    /// <summary>
    /// List of supported blockchain chains for this network
    /// e.g., ["AELF", "tDVV", "tDVW"]
    /// </summary>
    public List<string> SupportChain { get; set; } = new();
    
    /// <summary>
    /// Whitelist of addresses or patterns for enhanced security
    /// Used for additional validation and access control
    /// </summary>
    public List<string> SupportWhiteList { get; set; } = new();
}

/// <summary>
/// Base configuration containing common blockchain operation parameters
/// Shared across different operation types to eliminate duplication
/// </summary>
public class BaseBlockchainConfig
{
    /// <summary>
    /// Number of block confirmations required for security
    /// Can be numeric string or "auto" for dynamic confirmation
    /// </summary>
    public string MultiConfirm { get; set; }
    
    /// <summary>
    /// Time in seconds to wait for the required confirmations
    /// Used for calculating estimated completion time
    /// </summary>
    public decimal MultiConfirmSeconds { get; set; }
    
    /// <summary>
    /// Smart contract address for operations on this network
    /// Empty for native tokens
    /// </summary>
    public string ContractAddress { get; set; }
    
    /// <summary>
    /// Additional notes or warnings displayed to users
    /// </summary>
    public List<string> ExtraNotes { get; set; } = new();
    
    /// <summary>
    /// Whether operations are currently enabled
    /// Default: true
    /// </summary>
    public bool IsOpen { get; set; } = true;
}

/// <summary>
/// Basic network information and blockchain parameters
/// </summary>
public class NetworkInfo : BaseBlockchainConfig
{
    /// <summary>
    /// Network identifier (e.g., "Ethereum", "BSC", "Polygon")
    /// </summary>
    public string Network { get; set; }
    
    /// <summary>
    /// Human-readable network display name
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Average time in seconds for block generation on this network
    /// Used for transaction timing estimates
    /// </summary>
    public decimal BlockGenerationSeconds { get; set; }
    
    /// <summary>
    /// Average time in seconds a transaction stays in pending state
    /// Used for user experience and timeout calculations
    /// </summary>
    public decimal AveragePendingSeconds { get; set; }
    
    /// <summary>
    /// Minimum application version required to use this network
    /// Format: "1.0.0" or similar semantic versioning
    /// </summary>
    public string MinShowVersion { get; set; }
    
    /// <summary>
    /// Base URL for blockchain explorer
    /// Used to generate transaction and address links
    /// </summary>
    public string ExplorerUrl { get; set; }
    
    /// <summary>
    /// Whether this network enforces token access range restrictions
    /// Used for additional security and compliance checks
    /// </summary>
    public bool IsTokenAccessRange { get; set; }
    
    /// <summary>
    /// Current operational status of the network
    /// Values: "Active", "Maintenance", "Disabled"
    /// </summary>
    public string Status { get; set; }
    
    /// <summary>
    /// Minimum transaction amount for this network
    /// Specified as string to maintain precision
    /// </summary>
    public string MinAmount { get; set; }
    
    /// <summary>
    /// Liquidity pool address for token swaps (if applicable)
    /// Used for DEX operations and liquidity calculations
    /// </summary>
    public string PoolAddress { get; set; }
}

/// <summary>
/// Deposit operation configuration for a specific network
/// Inherits common blockchain parameters from BaseBlockchainConfig
/// </summary>
public class DepositInfo : BaseBlockchainConfig
{
    /// <summary>
    /// Minimum deposit amount allowed
    /// Specified as string to maintain precision
    /// </summary>
    public string MinDeposit { get; set; }
    
    /// <summary>
    /// Maximum deposit amount allowed per transaction
    /// Specified as string to maintain precision
    /// </summary>
    public string MaxDeposit { get; set; }
    
    /// <summary>
    /// Additional notes specific to swap operations during deposit
    /// </summary>
    public List<string> SwapExtraNotes { get; set; } = new();
    
    /// <summary>
    /// Supported deposit methods for this network
    /// e.g., ["direct", "swap", "bridge"]
    /// </summary>
    public List<string> SupportedMethods { get; set; } = new();
    
    /// <summary>
    /// Minimum confirmation time in seconds for fast deposits
    /// Used for urgent or express deposit processing
    /// </summary>
    public decimal? FastConfirmSeconds { get; set; }
}

/// <summary>
/// Withdrawal operation configuration for a specific network
/// Contains withdrawal-specific parameters with common blockchain config
/// </summary>
public class WithdrawInfo
{
    /// <summary>
    /// Whether withdrawal operations are currently enabled
    /// Default: true
    /// </summary>
    public bool IsOpen { get; set; } = true;
    
    /// <summary>
    /// Time in seconds to wait for withdrawal confirmations
    /// </summary>
    public decimal MultiConfirmSeconds { get; set; }
    
    /// <summary>
    /// Minimum withdrawal amount allowed
    /// Specified as string to maintain precision
    /// </summary>
    public string MinWithdraw { get; set; }
    
    /// <summary>
    /// Maximum withdrawal amount allowed per transaction
    /// Specified as string to maintain precision
    /// </summary>
    public string MaxWithdraw { get; set; }
    
    /// <summary>
    /// Standard withdrawal fee amount
    /// </summary>
    public decimal WithdrawFee { get; set; }
    
    /// <summary>
    /// Whether to use special fee display format
    /// Used for complex fee structures or promotional displays
    /// Default: false
    /// </summary>
    public bool SpecialWithdrawFeeDisplay { get; set; } = false;
    
    /// <summary>
    /// Special withdrawal fee display string
    /// Used when SpecialWithdrawFeeDisplay is true
    /// </summary>
    public string SpecialWithdrawFee { get; set; }
    
    /// <summary>
    /// Local network fee (gas fee) for the withdrawal transaction
    /// </summary>
    public decimal WithdrawLocalFee { get; set; }
    
    /// <summary>
    /// Unit of measurement for the local network fee
    /// e.g., "ETH", "BNB", "MATIC"
    /// </summary>
    public string WithdrawLocalFeeUnit { get; set; }
    
    /// <summary>
    /// Maximum withdrawal limit per 24-hour period
    /// Specified as string to maintain precision
    /// </summary>
    public string WithdrawLimit24h { get; set; }
    
    /// <summary>
    /// Number of decimal places for withdrawal amounts
    /// Used for UI formatting and validation
    /// </summary>
    public int Decimals { get; set; }
    
    /// <summary>
    /// Risk management configuration for withdrawals
    /// </summary>
    public WithdrawRiskConfig RiskConfig { get; set; } = new();
    
    /// <summary>
    /// Fee structure configuration
    /// </summary>
    public WithdrawFeeConfig FeeConfig { get; set; } = new();
}

/// <summary>
/// Risk management configuration for withdrawal operations
/// </summary>
public class WithdrawRiskConfig
{
    /// <summary>
    /// Maximum allowed withdrawal amount per transaction for risk control
    /// Null means no additional risk limit
    /// </summary>
    public decimal? MaxRiskAmount { get; set; }
    
    /// <summary>
    /// Requires additional verification for amounts above this threshold
    /// </summary>
    public decimal? VerificationThreshold { get; set; }
    
    /// <summary>
    /// Cooling period in seconds before next withdrawal
    /// Used for high-risk scenarios
    /// </summary>
    public int? CoolingPeriodSeconds { get; set; }
    
    /// <summary>
    /// Whether to enable real-time fraud detection
    /// </summary>
    public bool EnableFraudDetection { get; set; } = true;
}

/// <summary>
/// Fee structure configuration for withdrawal operations
/// </summary>
public class WithdrawFeeConfig
{
    /// <summary>
    /// Fee calculation method
    /// Values: "fixed", "percentage", "dynamic", "tiered"
    /// </summary>
    public string FeeType { get; set; } = "fixed";
    
    /// <summary>
    /// Base fee amount (for fixed fee type)
    /// </summary>
    public decimal BaseFee { get; set; }
    
    /// <summary>
    /// Fee percentage (for percentage fee type)
    /// </summary>
    public decimal FeePercentage { get; set; }
    
    /// <summary>
    /// Minimum fee amount regardless of calculation method
    /// </summary>
    public decimal MinFee { get; set; }
    
    /// <summary>
    /// Maximum fee amount regardless of calculation method
    /// </summary>
    public decimal MaxFee { get; set; }
    
    /// <summary>
    /// Tiered fee structure for different amount ranges
    /// Key: Amount threshold, Value: Fee configuration
    /// </summary>
    public Dictionary<decimal, decimal> TieredFees { get; set; } = new();
}