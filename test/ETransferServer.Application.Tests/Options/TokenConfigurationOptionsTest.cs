using System;
using System.Collections.Generic;
using System.Linq;
using ETransferServer.Options;
using Shouldly;
using Xunit;

namespace ETransferServer.Application.Tests.Options;

/// <summary>
/// Unit tests for TokenConfigurationOptions and related classes
/// Tests the new unified token configuration system
/// </summary>
public class TokenConfigurationOptionsTest
{
    [Fact]
    public void TokenConfigurationOptions_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var options = new TokenConfigurationOptions();

        // Assert
        options.CoreConfig.ShouldNotBeNull();
        options.AccessConfig.ShouldNotBeNull();
        options.OperationConfig.ShouldNotBeNull();
        options.SwapConfig.ShouldNotBeNull();
        options.SecurityConfig.ShouldNotBeNull();
        options.MetadataConfig.ShouldNotBeNull();
        options.MonitoringConfig.ShouldNotBeNull();
    }

    [Fact]
    public void TokenCoreConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var coreConfig = new TokenCoreConfig();

        // Assert
        coreConfig.TokensByOperation.ShouldNotBeNull();
        coreConfig.GlobalTokenRegistry.ShouldNotBeNull();
        coreConfig.DefaultProperties.ShouldNotBeNull();
        coreConfig.ValidationRules.ShouldNotBeNull();
    }

    [Fact]
    public void TokenDefinition_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var tokenDefinition = new TokenDefinition();

        // Assert
        tokenDefinition.ContractAddresses.ShouldNotBeNull();
        tokenDefinition.Status.ShouldNotBeNull();
        tokenDefinition.Metadata.ShouldNotBeNull();
        tokenDefinition.SupportedOperations.ShouldNotBeNull();
        tokenDefinition.ChainConfigs.ShouldNotBeNull();
    }

    [Fact]
    public void TokenStatus_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var tokenStatus = new TokenStatus();

        // Assert
        tokenStatus.IsActive.ShouldBeTrue();
        tokenStatus.IsListed.ShouldBeTrue();
        tokenStatus.SupportsDeposit.ShouldBeTrue();
        tokenStatus.SupportsWithdraw.ShouldBeTrue();
        tokenStatus.SupportsTransfer.ShouldBeTrue();
        tokenStatus.SupportsSwap.ShouldBeFalse();
    }

    [Fact]
    public void TokenAccessConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var accessConfig = new TokenAccessConfig();

        // Assert
        accessConfig.ReApplyHours.ShouldBe(48);
        accessConfig.DefaultConfig.ShouldNotBeNull();
        accessConfig.TokenConfigs.ShouldNotBeNull();
        accessConfig.ExternalServices.ShouldNotBeNull();
        accessConfig.ApprovalConfig.ShouldNotBeNull();
    }

    [Fact]
    public void TokenAccessDefaults_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var defaults = new TokenAccessDefaults();

        // Assert
        defaults.MinLiquidityUsd.ShouldBe(1000m);
        defaults.MinHolders.ShouldBe(100);
        defaults.MinMarketCapUsd.ShouldBe(10000m);
        defaults.MinDailyVolumeUsd.ShouldBe(1000m);
        defaults.AutoApproval.ShouldNotBeNull();
    }

    [Fact]
    public void TokenAutoApprovalThresholds_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var thresholds = new TokenAutoApprovalThresholds();

        // Assert
        thresholds.IsEnabled.ShouldBeFalse();
        thresholds.LiquidityThreshold.ShouldBe(100000m);
        thresholds.MarketCapThreshold.ShouldBe(1000000m);
        thresholds.HolderThreshold.ShouldBe(1000);
        thresholds.VolumeThreshold.ShouldBe(50000m);
    }

    [Fact]
    public void TokenOperationConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var operationConfig = new TokenOperationConfig();

        // Assert
        operationConfig.TokenOperations.ShouldNotBeNull();
        operationConfig.GlobalSettings.ShouldNotBeNull();
        operationConfig.OperationConfigs.ShouldNotBeNull();
    }

    [Fact]
    public void TokenOperationSupport_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var operationSupport = new TokenOperationSupport();

        // Assert
        operationSupport.DepositChains.ShouldNotBeNull();
        operationSupport.WithdrawChains.ShouldNotBeNull();
        operationSupport.TransferChains.ShouldNotBeNull();
        operationSupport.SwapChains.ShouldNotBeNull();
        operationSupport.CrossChain.ShouldNotBeNull();
    }

    [Fact]
    public void GlobalOperationSettings_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var globalSettings = new GlobalOperationSettings();

        // Assert
        globalSettings.EnableNewTokensByDefault.ShouldBeFalse();
        globalSettings.DefaultTimeoutSeconds.ShouldBe(300);
        globalSettings.MaxConcurrentOperations.ShouldBe(100);
        globalSettings.RateLimiting.ShouldNotBeNull();
    }

    [Fact]
    public void TokenSwapConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var swapConfig = new TokenSwapConfig();

        // Assert
        swapConfig.SwapPairs.ShouldNotBeNull();
        swapConfig.RoutingConfig.ShouldNotBeNull();
        swapConfig.FeeConfig.ShouldNotBeNull();
        swapConfig.PoolConfig.ShouldNotBeNull();
    }

    [Fact]
    public void TokenSwapPair_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var swapPair = new TokenSwapPair();

        // Assert
        swapPair.ToChains.ShouldNotBeNull();
        swapPair.IsActive.ShouldBeTrue();
        swapPair.MinSwapAmount.ShouldBe(0.001m);
        swapPair.MaxSwapAmount.ShouldBe(1000000m);
        swapPair.FeeRate.ShouldBe(0.003m);
        swapPair.SlippageTolerance.ShouldBe(0.05m);
    }

    [Fact]
    public void TokenSecurityConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var securityConfig = new TokenSecurityConfig();

        // Assert
        securityConfig.ContractSecurity.ShouldNotBeNull();
        securityConfig.AntiFraud.ShouldNotBeNull();
        securityConfig.Compliance.ShouldNotBeNull();
        securityConfig.Monitoring.ShouldNotBeNull();
    }

    [Fact]
    public void ContractSecurityConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var contractSecurity = new ContractSecurityConfig();

        // Assert
        contractSecurity.EnableContractVerification.ShouldBeTrue();
        contractSecurity.TrustedDeployers.ShouldNotBeNull();
        contractSecurity.BlacklistedContracts.ShouldNotBeNull();
        contractSecurity.RequiredFeatures.ShouldNotBeNull();
        contractSecurity.ScoreThresholds.ShouldNotBeNull();
    }

    [Fact]
    public void SecurityScoreThresholds_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var thresholds = new SecurityScoreThresholds();

        // Assert
        thresholds.MinSecurityScore.ShouldBe(70);
        thresholds.AutoApprovalScore.ShouldBe(90);
        thresholds.AutoRejectionScore.ShouldBe(30);
    }

    [Fact]
    public void AntiFraudConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var antiFraud = new AntiFraudConfig();

        // Assert
        antiFraud.IsEnabled.ShouldBeTrue();
        antiFraud.SuspiciousPatterns.ShouldNotBeNull();
        antiFraud.RiskWeights.ShouldNotBeNull();
        antiFraud.PreventionMeasures.ShouldNotBeNull();
    }

    [Fact]
    public void FraudPreventionMeasures_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var preventionMeasures = new FraudPreventionMeasures();

        // Assert
        preventionMeasures.HoneypotDetection.ShouldBeTrue();
        preventionMeasures.RugPullDetection.ShouldBeTrue();
        preventionMeasures.FlashLoanAttackDetection.ShouldBeTrue();
        preventionMeasures.PumpAndDumpDetection.ShouldBeTrue();
    }

    [Fact]
    public void TokenMetadataConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var metadataConfig = new TokenMetadataConfig();

        // Assert
        metadataConfig.SourceConfig.ShouldNotBeNull();
        metadataConfig.ValidationRules.ShouldNotBeNull();
        metadataConfig.CacheConfig.ShouldNotBeNull();
        metadataConfig.EnrichmentConfig.ShouldNotBeNull();
    }

    [Fact]
    public void MetadataCacheConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var cacheConfig = new MetadataCacheConfig();

        // Assert
        cacheConfig.IsEnabled.ShouldBeTrue();
        cacheConfig.DefaultExpirationSeconds.ShouldBe(3600);
        cacheConfig.MaxEntries.ShouldBe(10000);
    }

    [Fact]
    public void TokenMonitoringConfig_DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var monitoringConfig = new TokenMonitoringConfig();

        // Assert
        monitoringConfig.RealTimeMonitoring.ShouldNotBeNull();
        monitoringConfig.AlertConfig.ShouldNotBeNull();
        monitoringConfig.NotificationConfig.ShouldNotBeNull();
        monitoringConfig.PerformanceMonitoring.ShouldNotBeNull();
        monitoringConfig.AuditLogging.ShouldNotBeNull();
    }

    [Fact]
    public void RealTimeMonitoringConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var realTimeConfig = new RealTimeMonitoringConfig();

        // Assert
        realTimeConfig.IsEnabled.ShouldBeTrue();
        realTimeConfig.MonitoringIntervals.ShouldNotBeNull();
        realTimeConfig.RawDataRetentionDays.ShouldBe(7);
        realTimeConfig.AggregatedDataRetentionDays.ShouldBe(90);
    }

    [Fact]
    public void TokenAlertConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var alertConfig = new TokenAlertConfig();

        // Assert
        alertConfig.IsEnabled.ShouldBeTrue();
        alertConfig.AlertRules.ShouldNotBeNull();
        alertConfig.MaxAlertsPerMinute.ShouldBe(10);
        alertConfig.DuplicateSuppressionSeconds.ShouldBe(300);
    }

    [Fact]
    public void AuditLoggingConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var auditConfig = new AuditLoggingConfig();

        // Assert
        auditConfig.IsEnabled.ShouldBeTrue();
        auditConfig.LogLevel.ShouldBe("Information");
        auditConfig.AuditEvents.ShouldNotBeNull();
        auditConfig.RetentionDays.ShouldBe(90);
        auditConfig.ArchiveOldLogs.ShouldBeTrue();
    }

    [Fact]
    public void BackwardCompatibility_TokenOptions_ShouldBeInitialized()
    {
        // Arrange & Act
        var tokenOptions = new TokenOptions();

        // Assert
        tokenOptions.Deposit.ShouldNotBeNull();
        tokenOptions.Withdraw.ShouldNotBeNull();
        tokenOptions.Transfer.ShouldNotBeNull();
        tokenOptions.DepositSwap.ShouldNotBeNull();
    }

    [Fact]
    public void BackwardCompatibility_TokenConfig_ShouldWork()
    {
        // Arrange & Act
        var tokenConfig = new TokenConfig
        {
            Name = "Test Token",
            Symbol = "TEST",
            Decimals = 18,
            Icon = "test-icon.png",
            ContractAddress = "0x123456789"
        };

        // Assert
        tokenConfig.Name.ShouldBe("Test Token");
        tokenConfig.Symbol.ShouldBe("TEST");
        tokenConfig.Decimals.ShouldBe(18);
        tokenConfig.Icon.ShouldBe("test-icon.png");
        tokenConfig.ContractAddress.ShouldBe("0x123456789");
    }

    [Fact]
    public void BackwardCompatibility_TokenSwapConfigLegacy_ShouldWork()
    {
        // Arrange & Act
        var swapConfig = new TokenSwapConfigLegacy
        {
            Name = "USDT",
            Symbol = "USDT",
            Decimals = 6,
            ToTokenList = new List<ToTokenConfig>
            {
                new ToTokenConfig
                {
                    Symbol = "ELF",
                    Name = "ELF",
                    ChainIdList = new List<string> { "AELF", "tDVV" },
                    Icon = "elf-icon.png"
                }
            }
        };

        // Assert
        swapConfig.Name.ShouldBe("USDT");
        swapConfig.Symbol.ShouldBe("USDT");
        swapConfig.Decimals.ShouldBe(6);
        swapConfig.ToTokenList.ShouldNotBeNull();
        swapConfig.ToTokenList.Count.ShouldBe(1);
        swapConfig.ToTokenList[0].Symbol.ShouldBe("ELF");
        swapConfig.ToTokenList[0].ChainIdList.Count.ShouldBe(2);
    }

    [Fact]
    public void TokenValidationRules_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var validationRules = new TokenValidationRules();

        // Assert
        validationRules.RequiredFields.ShouldNotBeNull();
        validationRules.RequiredFields.ShouldContain("Name");
        validationRules.RequiredFields.ShouldContain("Symbol");
        validationRules.RequiredFields.ShouldContain("Decimals");
        validationRules.SymbolPattern.ShouldBe("^[A-Z][A-Z0-9-]{0,9}$");
        validationRules.MaxSymbolLength.ShouldBe(10);
        validationRules.MaxNameLength.ShouldBe(50);
        validationRules.DecimalRange.Min.ShouldBe(0);
        validationRules.DecimalRange.Max.ShouldBe(18);
        validationRules.BlacklistedSymbols.ShouldNotBeNull();
    }

    [Fact]
    public void TokenDefaultProperties_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var defaultProperties = new TokenDefaultProperties();

        // Assert
        defaultProperties.DefaultDecimals.ShouldBe(18);
        defaultProperties.DefaultIcon.ShouldBe("https://default-icon.example.com/token.png");
        defaultProperties.DefaultLimits.ShouldNotBeNull();
        defaultProperties.DefaultFees.ShouldNotBeNull();
        defaultProperties.DefaultOperations.ShouldNotBeNull();
        defaultProperties.DefaultOperations.ShouldContain("Deposit");
        defaultProperties.DefaultOperations.ShouldContain("Withdraw");
        defaultProperties.DefaultOperations.ShouldContain("Transfer");
    }

    [Fact]
    public void TokenAmountLimits_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var amountLimits = new TokenAmountLimits();

        // Assert
        amountLimits.MinDeposit.ShouldBe(0);
        amountLimits.MaxDeposit.ShouldBe(decimal.MaxValue);
        amountLimits.MinWithdraw.ShouldBe(0);
        amountLimits.MaxWithdraw.ShouldBe(decimal.MaxValue);
        amountLimits.DailyLimit.ShouldBe(decimal.MaxValue);
        amountLimits.MinTransfer.ShouldBe(0);
    }

    [Fact]
    public void TokenFeeConfig_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var feeConfig = new TokenFeeConfig();

        // Assert
        feeConfig.DepositFeeRate.ShouldBe(0);
        feeConfig.WithdrawFeeRate.ShouldBe(0);
        feeConfig.TransferFeeRate.ShouldBe(0);
        feeConfig.FixedFees.ShouldNotBeNull();
        feeConfig.GasFeeMultiplier.ShouldBe(1.0m);
    }

    [Fact]
    public void ComplexConfiguration_ShouldWork()
    {
        // Arrange
        var config = new TokenConfigurationOptions
        {
            CoreConfig = new TokenCoreConfig
            {
                GlobalTokenRegistry = new Dictionary<string, TokenDefinition>
                {
                    ["USDT"] = new TokenDefinition
                    {
                        Name = "Tether USD",
                        Symbol = "USDT",
                        Decimals = 6,
                        Icon = "usdt-icon.png",
                        Status = new TokenStatus
                        {
                            IsActive = true,
                            SupportsDeposit = true,
                            SupportsWithdraw = true,
                            SupportsSwap = true
                        },
                        ChainConfigs = new Dictionary<string, TokenChainConfig>
                        {
                            ["AELF"] = new TokenChainConfig
                            {
                                ContractAddress = "0xaelf123",
                                AmountLimits = new TokenAmountLimits
                                {
                                    MinDeposit = 1m,
                                    MaxDeposit = 100000m
                                }
                            }
                        }
                    }
                }
            }
        };

        // Act & Assert
        config.CoreConfig.GlobalTokenRegistry.ShouldContainKey("USDT");
        var usdtToken = config.CoreConfig.GlobalTokenRegistry["USDT"];
        usdtToken.Name.ShouldBe("Tether USD");
        usdtToken.Symbol.ShouldBe("USDT");
        usdtToken.Decimals.ShouldBe(6);
        usdtToken.Status.IsActive.ShouldBeTrue();
        usdtToken.Status.SupportsSwap.ShouldBeTrue();
        usdtToken.ChainConfigs.ShouldContainKey("AELF");
        usdtToken.ChainConfigs["AELF"].ContractAddress.ShouldBe("0xaelf123");
        usdtToken.ChainConfigs["AELF"].AmountLimits.MinDeposit.ShouldBe(1m);
        usdtToken.ChainConfigs["AELF"].AmountLimits.MaxDeposit.ShouldBe(100000m);
    }
} 