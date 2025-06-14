using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.Extensions;

/// <summary>
/// Token configuration factory providing unified access to token-related settings
/// Consolidates all token configuration operations and business logic
/// </summary>
public class TokenConfigurationFactory
{
    private readonly IOptionsSnapshot<TokenConfigurationOptions> _tokenConfig;
    private readonly ILogger<TokenConfigurationFactory> _logger;

    public TokenConfigurationFactory(
        IOptionsSnapshot<TokenConfigurationOptions> tokenConfig,
        ILogger<TokenConfigurationFactory> logger)
    {
        _tokenConfig = tokenConfig;
        _logger = logger;
    }

    #region Token Core Management

    /// <summary>
    /// Get token definition by symbol
    /// </summary>
    public TokenDefinition? GetTokenDefinition(string symbol)
    {
        var config = _tokenConfig.Value;
        return config.CoreConfig.GlobalTokenRegistry.GetValueOrDefault(symbol?.ToUpper());
    }

    /// <summary>
    /// Get all tokens for specific operation and chain
    /// </summary>
    public List<TokenDefinition> GetTokensByOperation(string operationType, string chainId)
    {
        var config = _tokenConfig.Value;
        
        if (!config.CoreConfig.TokensByOperation.TryGetValue(operationType, out var operationTokens))
            return new List<TokenDefinition>();

        if (!operationTokens.TryGetValue(chainId, out var tokens))
            return new List<TokenDefinition>();

        return tokens;
    }

    /// <summary>
    /// Check if token supports specific operation on chain
    /// </summary>
    public bool SupportsOperation(string symbol, string operationType, string chainId)
    {
        var tokenDefinition = GetTokenDefinition(symbol);
        if (tokenDefinition == null) return false;

        // Check global support
        if (!tokenDefinition.SupportedOperations.Contains(operationType))
            return false;

        // Check chain-specific support
        if (!tokenDefinition.ChainConfigs.TryGetValue(chainId, out var chainConfig))
            return false;

        return operationType.ToLower() switch
        {
            "deposit" => tokenDefinition.Status.SupportsDeposit,
            "withdraw" => tokenDefinition.Status.SupportsWithdraw,
            "transfer" => tokenDefinition.Status.SupportsTransfer,
            "swap" => tokenDefinition.Status.SupportsSwap,
            _ => false
        };
    }

    /// <summary>
    /// Validate token configuration
    /// </summary>
    public (bool IsValid, List<string> Errors) ValidateTokenConfiguration(TokenDefinition token)
    {
        var errors = new List<string>();
        var rules = _tokenConfig.Value.CoreConfig.ValidationRules;

        // Check required fields
        foreach (var field in rules.RequiredFields)
        {
            var value = GetTokenProperty(token, field);
            if (string.IsNullOrEmpty(value))
                errors.Add($"Required field '{field}' is missing");
        }

        // Validate symbol format
        if (!string.IsNullOrEmpty(token.Symbol) && 
            !System.Text.RegularExpressions.Regex.IsMatch(token.Symbol, rules.SymbolPattern))
        {
            errors.Add($"Symbol '{token.Symbol}' does not match required pattern");
        }

        // Check symbol length
        if (token.Symbol?.Length > rules.MaxSymbolLength)
            errors.Add($"Symbol length exceeds maximum of {rules.MaxSymbolLength}");

        // Check name length
        if (token.Name?.Length > rules.MaxNameLength)
            errors.Add($"Name length exceeds maximum of {rules.MaxNameLength}");

        // Validate decimals range
        if (token.Decimals < rules.DecimalRange.Min || token.Decimals > rules.DecimalRange.Max)
            errors.Add($"Decimals must be between {rules.DecimalRange.Min} and {rules.DecimalRange.Max}");

        // Check blacklisted symbols
        if (rules.BlacklistedSymbols.Contains(token.Symbol?.ToUpper()))
            errors.Add($"Symbol '{token.Symbol}' is blacklisted");

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Get token amount limits for specific chain
    /// </summary>
    public TokenAmountLimits? GetAmountLimits(string symbol, string chainId)
    {
        var token = GetTokenDefinition(symbol);
        if (token?.ChainConfigs.TryGetValue(chainId, out var chainConfig) == true)
            return chainConfig.AmountLimits;

        return _tokenConfig.Value.CoreConfig.DefaultProperties.DefaultLimits;
    }

    #endregion

    #region Token Access Management

    /// <summary>
    /// Check if token meets listing requirements
    /// </summary>
    public async Task<(bool Approved, string Reason)> EvaluateTokenListing(string symbol)
    {
        var token = GetTokenDefinition(symbol);
        if (token == null)
            return (false, "Token not found");

        var accessConfig = _tokenConfig.Value.AccessConfig;
        var requirements = accessConfig.DefaultConfig;

        // Check if token has specific requirements
        if (accessConfig.TokenConfigs.TryGetValue(symbol, out var specificConfig))
            requirements = specificConfig;

        // Simulate external data fetching (liquidity, holders, etc.)
        var tokenMetrics = await GetTokenMetrics(symbol);

        if (tokenMetrics.LiquidityUsd < requirements.MinLiquidityUsd)
            return (false, $"Insufficient liquidity: {tokenMetrics.LiquidityUsd} < {requirements.MinLiquidityUsd}");

        if (tokenMetrics.HolderCount < requirements.MinHolders)
            return (false, $"Insufficient holders: {tokenMetrics.HolderCount} < {requirements.MinHolders}");

        if (tokenMetrics.MarketCapUsd < requirements.MinMarketCapUsd)
            return (false, $"Insufficient market cap: {tokenMetrics.MarketCapUsd} < {requirements.MinMarketCapUsd}");

        // Check auto-approval thresholds
        var autoApproval = requirements.AutoApproval;
        if (autoApproval.IsEnabled &&
            tokenMetrics.LiquidityUsd >= autoApproval.LiquidityThreshold &&
            tokenMetrics.MarketCapUsd >= autoApproval.MarketCapThreshold &&
            tokenMetrics.HolderCount >= autoApproval.HolderThreshold)
        {
            return (true, "Auto-approved based on metrics");
        }

        return (true, "Manual review required");
    }

    /// <summary>
    /// Get token access configuration
    /// </summary>
    public TokenAccessDefaults GetAccessConfig(string symbol)
    {
        var accessConfig = _tokenConfig.Value.AccessConfig;
        
        return accessConfig.TokenConfigs.TryGetValue(symbol, out var config) 
            ? config 
            : accessConfig.DefaultConfig;
    }

    #endregion

    #region Token Operation Support

    /// <summary>
    /// Get supported chains for token operation
    /// </summary>
    public List<string> GetSupportedChains(string symbol, string operationType)
    {
        var operationConfig = _tokenConfig.Value.OperationConfig;
        
        if (!operationConfig.TokenOperations.TryGetValue(symbol, out var tokenOps))
            return new List<string>();

        return operationType.ToLower() switch
        {
            "deposit" => tokenOps.DepositChains,
            "withdraw" => tokenOps.WithdrawChains,
            "transfer" => tokenOps.TransferChains,
            "swap" => tokenOps.SwapChains,
            _ => new List<string>()
        };
    }

    /// <summary>
    /// Get cross-chain routes for token
    /// </summary>
    public List<CrossChainRoute> GetCrossChainRoutes(string symbol, string fromChain, string toChain)
    {
        var operationConfig = _tokenConfig.Value.OperationConfig;
        
        if (!operationConfig.TokenOperations.TryGetValue(symbol, out var tokenOps))
            return new List<CrossChainRoute>();

        return tokenOps.CrossChain.TransferRoutes
            .Where(r => r.FromChain == fromChain && r.ToChain == toChain && r.IsActive)
            .ToList();
    }

    /// <summary>
    /// Calculate cross-chain fee
    /// </summary>
    public decimal CalculateCrossChainFee(string symbol, string fromChain, string toChain, decimal amount)
    {
        var operationConfig = _tokenConfig.Value.OperationConfig;
        
        if (!operationConfig.TokenOperations.TryGetValue(symbol, out var tokenOps))
            return 0;

        var feeConfig = tokenOps.CrossChain.FeeConfig;
        var baseFee = feeConfig.BaseFee;
        var percentageFee = amount * feeConfig.FeePercentage;
        
        // Apply chain-specific multipliers
        var multiplier = 1.0m;
        if (feeConfig.ChainFeeMultipliers.TryGetValue(fromChain, out var fromMultiplier))
            multiplier *= fromMultiplier;
        if (feeConfig.ChainFeeMultipliers.TryGetValue(toChain, out var toMultiplier))
            multiplier *= toMultiplier;

        var totalFee = (baseFee + percentageFee) * multiplier;
        
        // Apply min/max limits
        return Math.Max(feeConfig.MinFee, Math.Min(feeConfig.MaxFee, totalFee));
    }

    #endregion

    #region Token Swap Management

    /// <summary>
    /// Get available swap pairs for token
    /// </summary>
    public List<TokenSwapPair> GetSwapPairs(string fromToken)
    {
        var swapConfig = _tokenConfig.Value.SwapConfig;
        return swapConfig.SwapPairs
            .Where(p => p.FromToken.Equals(fromToken, StringComparison.OrdinalIgnoreCase) && p.IsActive)
            .ToList();
    }

    /// <summary>
    /// Calculate swap fee
    /// </summary>
    public decimal CalculateSwapFee(string fromToken, string toToken, decimal amount, decimal volume24h = 0)
    {
        var swapPair = GetSwapPairs(fromToken)
            .FirstOrDefault(p => p.ToToken.Equals(toToken, StringComparison.OrdinalIgnoreCase));
        
        if (swapPair == null)
            return 0;

        var baseFee = amount * swapPair.FeeRate;
        
        // Apply volume discounts
        var feeConfig = _tokenConfig.Value.SwapConfig.FeeConfig;
        var discount = GetVolumeDiscount(volume24h, feeConfig.VolumeDiscounts);
        
        return baseFee * (1 - discount);
    }

    /// <summary>
    /// Find optimal swap route
    /// </summary>
    public async Task<SwapRoute?> FindOptimalSwapRoute(string fromToken, string toToken, decimal amount)
    {
        var routingConfig = _tokenConfig.Value.SwapConfig.RoutingConfig;
        
        // Check cache first
        var cacheKey = $"{fromToken}-{toToken}-{amount}";
        if (routingConfig.CacheConfig.IsEnabled)
        {
            var cachedRoute = await GetCachedRoute(cacheKey);
            if (cachedRoute != null)
                return cachedRoute;
        }

        // Find direct route
        var directPair = GetSwapPairs(fromToken)
            .FirstOrDefault(p => p.ToToken.Equals(toToken, StringComparison.OrdinalIgnoreCase));
        
        if (directPair != null)
        {
            var route = new SwapRoute
            {
                FromToken = fromToken,
                ToToken = toToken,
                Hops = new List<SwapHop> 
                { 
                    new() { FromToken = fromToken, ToToken = toToken, Fee = directPair.FeeRate } 
                },
                TotalFee = CalculateSwapFee(fromToken, toToken, amount),
                EstimatedOutputAmount = amount * (1 - directPair.FeeRate)
            };

            await CacheRoute(cacheKey, route);
            return route;
        }

        // Find multi-hop routes if direct route not available
        return await FindMultiHopRoute(fromToken, toToken, amount, routingConfig.MaxHops);
    }

    #endregion

    #region Token Security

    /// <summary>
    /// Perform security assessment
    /// </summary>
    public async Task<SecurityAssessmentResult> AssessTokenSecurity(string symbol)
    {
        var token = GetTokenDefinition(symbol);
        if (token == null)
            return new SecurityAssessmentResult { Score = 0, Warnings = new[] { "Token not found" }.ToList() };

        var securityConfig = _tokenConfig.Value.SecurityConfig;
        var result = new SecurityAssessmentResult();

        // Contract security checks
        var contractScore = await AssessContractSecurity(token, securityConfig.ContractSecurity);
        result.Score += contractScore * 0.4m; // 40% weight

        // Anti-fraud checks
        var fraudScore = await AssessAntiFraud(token, securityConfig.AntiFraud);
        result.Score += fraudScore * 0.3m; // 30% weight

        // Compliance checks
        var complianceScore = await AssessCompliance(token, securityConfig.Compliance);
        result.Score += complianceScore * 0.3m; // 30% weight

        // Determine overall assessment
        var thresholds = securityConfig.ContractSecurity.ScoreThresholds;
        if (result.Score >= thresholds.AutoApprovalScore)
            result.Assessment = "AUTO_APPROVED";
        else if (result.Score >= thresholds.MinSecurityScore)
            result.Assessment = "MANUAL_REVIEW";
        else
            result.Assessment = "REJECTED";

        return result;
    }

    /// <summary>
    /// Check if token is high risk
    /// </summary>
    public bool IsHighRiskToken(string symbol, string operation, decimal amount)
    {
        var securityConfig = _tokenConfig.Value.SecurityConfig;
        var antiFraud = securityConfig.AntiFraud;

        if (!antiFraud.IsEnabled)
            return false;

        // Check against suspicious patterns
        foreach (var pattern in antiFraud.SuspiciousPatterns)
        {
            if (symbol.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Calculate risk score
        var riskScore = CalculateRiskScore(symbol, operation, amount, antiFraud.RiskWeights);
        return riskScore > 0.7m; // High risk threshold
    }

    #endregion

    #region Token Monitoring

    /// <summary>
    /// Check if token operation should trigger alert
    /// </summary>
    public async Task<List<TokenAlert>> CheckAlertConditions(string symbol, string operation, decimal amount, Dictionary<string, object> context)
    {
        var alerts = new List<TokenAlert>();
        var alertConfig = _tokenConfig.Value.MonitoringConfig.AlertConfig;

        if (!alertConfig.IsEnabled)
            return alerts;

        foreach (var rule in alertConfig.AlertRules.Where(r => r.IsActive))
        {
            // Check if rule applies to this token
            if (!rule.TokenPatterns.Any(pattern => IsTokenMatch(symbol, pattern)))
                continue;

            // Evaluate conditions
            bool allConditionsMet = true;
            foreach (var condition in rule.Conditions)
            {
                if (!await EvaluateAlertCondition(condition, symbol, operation, amount, context))
                {
                    allConditionsMet = false;
                    break;
                }
            }

            if (allConditionsMet)
            {
                alerts.Add(new TokenAlert
                {
                    RuleId = rule.RuleId,
                    RuleName = rule.Name,
                    Severity = rule.Severity,
                    Message = $"Alert triggered for {symbol}: {rule.Description}",
                    Context = context,
                    Actions = rule.Actions
                });
            }
        }

        return alerts;
    }

    /// <summary>
    /// Get performance metrics for token
    /// </summary>
    public async Task<Dictionary<string, decimal>> GetTokenPerformanceMetrics(string symbol)
    {
        var performanceConfig = _tokenConfig.Value.MonitoringConfig.PerformanceMonitoring;
        var metrics = new Dictionary<string, decimal>();

        if (!performanceConfig.IsEnabled)
            return metrics;

        foreach (var metric in performanceConfig.Metrics)
        {
            var value = await CollectMetric(symbol, metric);
            metrics[metric.MetricId] = value;
        }

        return metrics;
    }

    #endregion

    #region Helper Methods

    private string? GetTokenProperty(TokenDefinition token, string propertyName)
    {
        return propertyName.ToLower() switch
        {
            "name" => token.Name,
            "symbol" => token.Symbol,
            "icon" => token.Icon,
            _ => null
        };
    }

    private async Task<TokenMetrics> GetTokenMetrics(string symbol)
    {
        // Simulate fetching real metrics from external sources
        await Task.Delay(100); // Simulate API call
        
        return new TokenMetrics
        {
            LiquidityUsd = Random.Shared.Next(1000, 100000),
            HolderCount = Random.Shared.Next(100, 10000),
            MarketCapUsd = Random.Shared.Next(10000, 1000000),
            DailyVolumeUsd = Random.Shared.Next(1000, 50000)
        };
    }

    private decimal GetVolumeDiscount(decimal volume24h, Dictionary<decimal, decimal> volumeDiscounts)
    {
        decimal discount = 0;
        foreach (var kvp in volumeDiscounts.OrderByDescending(x => x.Key))
        {
            if (volume24h >= kvp.Key)
            {
                discount = kvp.Value;
                break;
            }
        }
        return discount;
    }

    private async Task<SwapRoute?> GetCachedRoute(string cacheKey)
    {
        // Implement cache lookup logic
        await Task.Delay(10);
        return null; // Placeholder
    }

    private async Task CacheRoute(string cacheKey, SwapRoute route)
    {
        // Implement cache storage logic
        await Task.Delay(10);
    }

    private async Task<SwapRoute?> FindMultiHopRoute(string fromToken, string toToken, decimal amount, int maxHops)
    {
        // Implement multi-hop routing algorithm
        await Task.Delay(100);
        return null; // Placeholder for complex routing logic
    }

    private async Task<decimal> AssessContractSecurity(TokenDefinition token, ContractSecurityConfig config)
    {
        // Implement contract security assessment
        await Task.Delay(50);
        return 0.8m; // Placeholder score
    }

    private async Task<decimal> AssessAntiFraud(TokenDefinition token, AntiFraudConfig config)
    {
        // Implement anti-fraud assessment
        await Task.Delay(50);
        return 0.7m; // Placeholder score
    }

    private async Task<decimal> AssessCompliance(TokenDefinition token, ComplianceConfig config)
    {
        // Implement compliance assessment
        await Task.Delay(50);
        return 0.9m; // Placeholder score
    }

    private decimal CalculateRiskScore(string symbol, string operation, decimal amount, RiskScoreWeights weights)
    {
        // Implement risk score calculation
        return 0.3m; // Placeholder
    }

    private bool IsTokenMatch(string symbol, string pattern)
    {
        // Implement pattern matching logic (regex, wildcards, etc.)
        return pattern == "*" || pattern.Equals(symbol, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> EvaluateAlertCondition(AlertCondition condition, string symbol, string operation, decimal amount, Dictionary<string, object> context)
    {
        // Implement condition evaluation logic
        await Task.Delay(10);
        return false; // Placeholder
    }

    private async Task<decimal> CollectMetric(string symbol, PerformanceMetric metric)
    {
        // Implement metric collection logic
        await Task.Delay(20);
        return Random.Shared.Next(0, 100); // Placeholder
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Token metrics from external sources
/// </summary>
public class TokenMetrics
{
    public decimal LiquidityUsd { get; set; }
    public int HolderCount { get; set; }
    public decimal MarketCapUsd { get; set; }
    public decimal DailyVolumeUsd { get; set; }
}

/// <summary>
/// Security assessment result
/// </summary>
public class SecurityAssessmentResult
{
    public decimal Score { get; set; }
    public string Assessment { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Details { get; set; } = new();
}

/// <summary>
/// Swap route definition
/// </summary>
public class SwapRoute
{
    public string FromToken { get; set; } = "";
    public string ToToken { get; set; } = "";
    public List<SwapHop> Hops { get; set; } = new();
    public decimal TotalFee { get; set; }
    public decimal EstimatedOutputAmount { get; set; }
    public int EstimatedTimeSeconds { get; set; }
}

/// <summary>
/// Swap hop in a multi-hop route
/// </summary>
public class SwapHop
{
    public string FromToken { get; set; } = "";
    public string ToToken { get; set; } = "";
    public decimal Fee { get; set; }
    public string Protocol { get; set; } = "";
}

/// <summary>
/// Token alert information
/// </summary>
public class TokenAlert
{
    public string RuleId { get; set; } = "";
    public string RuleName { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Message { get; set; } = "";
    public Dictionary<string, object> Context { get; set; } = new();
    public List<AlertAction> Actions { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion 