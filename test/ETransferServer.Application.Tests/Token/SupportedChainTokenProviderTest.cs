using System;
using System.Collections.Generic;
using System.Linq;
using ETransferServer.Common;
using ETransferServer.Options;
using ETransferServer.Token.Dtos;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using ToTokenConfig = ETransferServer.Options.ToTokenConfig;

namespace ETransferServer.Token;

/// <summary>
/// Unit tests for SupportedChainTokenProvider
/// Tests token configuration, deposit support, swap support, and token list retrieval
/// </summary>
public class SupportedChainTokenProviderTest
{
    private readonly Mock<IOptionsSnapshot<SupportedTokenSwapOptions>> _mockSupportedTokenSwapOptions;
    private readonly Mock<IOptionsSnapshot<TokenInfoOptions>> _mockTokenInfoOptions;
    private readonly Mock<IOptionsSnapshot<SupportedChainTokensOptions>> _mockSupportedChainTokensOptions;
    private readonly SupportedChainTokenProvider _provider;

    public SupportedChainTokenProviderTest()
    {
        _mockSupportedTokenSwapOptions = new Mock<IOptionsSnapshot<SupportedTokenSwapOptions>>();
        _mockTokenInfoOptions = new Mock<IOptionsSnapshot<TokenInfoOptions>>();
        _mockSupportedChainTokensOptions = new Mock<IOptionsSnapshot<SupportedChainTokensOptions>>();

        // Setup default mock data
        SetupMockData();

        _provider = new SupportedChainTokenProvider(
            _mockSupportedTokenSwapOptions.Object,
            _mockTokenInfoOptions.Object,
            _mockSupportedChainTokensOptions.Object
        );
    }

    [Fact]
    public void GetTokenConfig_WithSpecificSymbol_ShouldReturnSingleToken()
    {
        // Act
        var result = _provider.GetTokenConfig("USDT");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        var tokenConfig = result.First();
        tokenConfig.Symbol.ShouldBe("USDT");
        tokenConfig.Name.ShouldBe("Tether USD");
        tokenConfig.Decimals.ShouldBe(6);
        tokenConfig.Icon.ShouldBe("usdt-icon.png");
        tokenConfig.ContractAddress.ShouldBe("0xusdt123");
        tokenConfig.ToTokenList.ShouldNotBeNull();
        tokenConfig.ToTokenList.Count.ShouldBe(1);
        tokenConfig.ToTokenList[0].Symbol.ShouldBe("ELF");
        tokenConfig.ToTokenList[0].ChainIdList.ShouldContain("AELF");
    }

    [Fact]
    public void GetTokenConfig_WithNullSymbol_ShouldReturnAllTokens()
    {
        // Act
        var result = _provider.GetTokenConfig(null);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2); // USDT and ELF
        result.Any(t => t.Symbol == "USDT").ShouldBeTrue();
        result.Any(t => t.Symbol == "ELF").ShouldBeTrue();
    }

    [Fact]
    public void GetTokenConfig_WithEmptySymbol_ShouldReturnAllTokens()
    {
        // Act
        var result = _provider.GetTokenConfig("");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2); // USDT and ELF
    }

    [Fact]
    public void GetTokenConfig_WithNonExistentSymbol_ShouldReturnEmptyList()
    {
        // Act
        var result = _provider.GetTokenConfig("NONEXISTENT");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void GetTokenConfig_WhenTokenNotInSwapMap_ShouldSkipToken()
    {
        // Arrange
        var swapOptions = new SupportedTokenSwapOptions
        {
            SwapTokenMap = new Dictionary<string, List<ToTokenConfig>>()
        };
        _mockSupportedTokenSwapOptions.Setup(x => x.Value).Returns(swapOptions);

        // Act
        var result = _provider.GetTokenConfig("USDT");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void GetTokenConfig_WhenTargetTokenNotInTokenInfo_ShouldSkipToToken()
    {
        // Arrange
        var tokenInfoOptions = new TokenInfoOptions
        {
            Tokens = new Dictionary<string, TokenInfoDto>
            {
                ["USDT"] = new TokenInfoDto
                {
                    Symbol = "USDT",
                    Name = "Tether USD",
                    Decimal = 6,
                    Icon = "usdt-icon.png",
                    TokenAddress = "0xusdt123"
                }
                // ELF token info is missing
            }
        };
        _mockTokenInfoOptions.Setup(x => x.Value).Returns(tokenInfoOptions);

        // Act
        var result = _provider.GetTokenConfig("USDT");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].ToTokenList.Count.ShouldBe(0); // ELF should be skipped
    }

    [Fact]
    public void IsTokenSupportedDeposit_NoDepositSwap_SameSymbols_ShouldCheckSwapMap()
    {
        // Act - NoDepositSwap returns true when symbols are same
        var result = _provider.IsTokenSupportedDeposit("USDT", "USDT", "AELF");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsTokenSupportedDeposit_NoDepositSwap_NullToSymbol_ShouldCheckSwapMap()
    {
        // Act - NoDepositSwap returns true when toSymbol is null
        var result = _provider.IsTokenSupportedDeposit("USDT", null, "AELF");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsTokenSupportedDeposit_IsDepositSwap_ValidSwap_ShouldReturnTrue()
    {
        // Act - IsDepositSwap returns true when symbols are different
        var result = _provider.IsTokenSupportedDeposit("USDT", "ELF", "AELF");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsTokenSupportedDeposit_IsDepositSwap_InvalidSwap_ShouldReturnFalse()
    {
        // Act - Try to swap to unsupported token
        var result = _provider.IsTokenSupportedDeposit("USDT", "NONEXISTENT", "AELF");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTokenSupportedSwap_ValidSwap_ShouldReturnTrue()
    {
        // Act
        var result = _provider.IsTokenSupportedSwap("USDT", "ELF", "AELF");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsTokenSupportedSwap_NoSwapScenario_SameSymbols_ShouldReturnFalse()
    {
        // Act - Same symbols means no swap
        var result = _provider.IsTokenSupportedSwap("USDT", "USDT", "AELF");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTokenSupportedSwap_NoSwapScenario_NullToSymbol_ShouldReturnFalse()
    {
        // Act - Null toSymbol means no swap
        var result = _provider.IsTokenSupportedSwap("USDT", null, "AELF");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTokenSupportedSwap_InvalidTokenPair_ShouldReturnFalse()
    {
        // Act - Try to swap non-existent token pair
        var result = _provider.IsTokenSupportedSwap("NONEXISTENT", "ELF", "AELF");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsTokenSupportedSwap_UnsupportedChain_ShouldReturnFalse()
    {
        // Act - Try to swap to unsupported chain
        var result = _provider.IsTokenSupportedSwap("USDT", "ELF", "UNSUPPORTED_CHAIN");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void GetTokenListByType_DepositType_ShouldReturnDepositTokens()
    {
        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Deposit.ToString(), "AELF");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Symbol.ShouldBe("USDT");
        result[0].Name.ShouldBe("Tether USD");
        result[0].Decimals.ShouldBe(6);
        result[0].Icon.ShouldBe("usdt-icon.png");
        result[0].ContractAddress.ShouldBe("0xusdt123");
    }

    [Fact]
    public void GetTokenListByType_WithdrawType_ShouldReturnWithdrawTokens()
    {
        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Withdraw.ToString(), "AELF");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Symbol.ShouldBe("ELF");
        result[0].Name.ShouldBe("AELF Token");
        result[0].Decimals.ShouldBe(8);
    }

    [Fact]
    public void GetTokenListByType_TransferType_ShouldReturnTransferTokens()
    {
        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Transfer.ToString(), null);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Symbol.ShouldBe("USDT");
        result[0].Name.ShouldBe("Tether USD");
    }

    [Fact]
    public void GetTokenListByType_UnknownType_ShouldReturnTransferTokens()
    {
        // Act
        var result = _provider.GetTokenListByType("UNKNOWN", null);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
        result[0].Symbol.ShouldBe("USDT");
    }

    [Fact]
    public void GetTokenListByType_EmptyTokens_ShouldReturnEmptyList()
    {
        // Arrange
        var supportedChainTokensOptions = new SupportedChainTokensOptions
        {
            Tokens = new Dictionary<string, SupportTokenInfo>
            {
                ["AELF"] = new SupportTokenInfo
                {
                    Deposit = new Dictionary<string, StatusInfo>(),
                    Withdraw = new Dictionary<string, StatusInfo>()
                }
            },
            Transfer = new Dictionary<string, StatusInfo>()
        };
        _mockSupportedChainTokensOptions.Setup(x => x.Value).Returns(supportedChainTokensOptions);

        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Deposit.ToString(), "AELF");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void GetTokenListByType_TokenNotInTokenInfo_ShouldSkipToken()
    {
        // Arrange
        var tokenInfoOptions = new TokenInfoOptions
        {
            Tokens = new Dictionary<string, TokenInfoDto>()
            // Empty - no token info
        };
        _mockTokenInfoOptions.Setup(x => x.Value).Returns(tokenInfoOptions);

        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Deposit.ToString(), "AELF");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void GetTokenListByType_NullTokens_ShouldReturnEmptyList()
    {
        // Arrange
        var supportedChainTokensOptions = new SupportedChainTokensOptions
        {
            Tokens = new Dictionary<string, SupportTokenInfo>
            {
                ["AELF"] = new SupportTokenInfo
                {
                    Deposit = null, // Null tokens
                    Withdraw = null
                }
            },
            Transfer = null
        };
        _mockSupportedChainTokensOptions.Setup(x => x.Value).Returns(supportedChainTokensOptions);

        // Act
        var result = _provider.GetTokenListByType(OrderTypeEnum.Deposit.ToString(), "AELF");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    private void SetupMockData()
    {
        // Setup SupportedTokenSwapOptions
        var swapOptions = new SupportedTokenSwapOptions
        {
            SwapTokenMap = new Dictionary<string, List<ToTokenConfig>>
            {
                ["USDT"] = new List<ToTokenConfig>
                {
                    new ToTokenConfig
                    {
                        Symbol = "ELF",
                        ChainIdList = new List<string> { "AELF", "tDVV" }
                    }
                },
                ["ELF"] = new List<ToTokenConfig>
                {
                    new ToTokenConfig
                    {
                        Symbol = "USDT",
                        ChainIdList = new List<string> { "AELF", "tDVV" }
                    }
                }
            }
        };
        _mockSupportedTokenSwapOptions.Setup(x => x.Value).Returns(swapOptions);

        // Setup TokenInfoOptions
        var tokenInfoOptions = new TokenInfoOptions
        {
            Tokens = new Dictionary<string, TokenInfoDto>
            {
                ["USDT"] = new TokenInfoDto
                {
                    Symbol = "USDT",
                    Name = "Tether USD",
                    Decimal = 6,
                    Icon = "usdt-icon.png",
                    TokenAddress = "0xusdt123"
                },
                ["ELF"] = new TokenInfoDto
                {
                    Symbol = "ELF",
                    Name = "AELF Token",
                    Decimal = 8,
                    Icon = "elf-icon.png",
                    TokenAddress = "0xelf456"
                }
            }
        };
        _mockTokenInfoOptions.Setup(x => x.Value).Returns(tokenInfoOptions);

        // Setup SupportedChainTokensOptions
        var supportedChainTokensOptions = new SupportedChainTokensOptions
        {
            Tokens = new Dictionary<string, SupportTokenInfo>
            {
                ["AELF"] = new SupportTokenInfo
                {
                    Deposit = new Dictionary<string, StatusInfo>
                    {
                        ["USDT"] = new StatusInfo { IsOpen = true }
                    },
                    Withdraw = new Dictionary<string, StatusInfo>
                    {
                        ["ELF"] = new StatusInfo { IsOpen = true }
                    }
                }
            },
            Transfer = new Dictionary<string, StatusInfo>
            {
                ["USDT"] = new StatusInfo { IsOpen = true }
            }
        };
        _mockSupportedChainTokensOptions.Setup(x => x.Value).Returns(supportedChainTokensOptions);
    }
} 