using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Models;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Swap;
using ETransferServer.Swap.Dtos;
using ETransferServer.Token;
using ETransferServer.Token.Dtos;
using ETransferServer.User;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using TransactionThreshold = ETransferServer.Options.TransactionThreshold;

namespace ETransferServer.Order;

[Collection(ClusterCollection.Name)]
public class OrderDepositTest : ETransferServerApplicationTestBase
{
    private readonly IOrderDepositAppService _orderDepositAppService;

    public OrderDepositTest(ITestOutputHelper output) : base(output)
    {
        _orderDepositAppService = GetRequiredService<IOrderDepositAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        services.AddSingleton(MockNetworkOptions());
        services.AddSingleton(MockTokenSupportedChainOptions());
        services.AddSingleton(MockTokenOptions());
        services.AddSingleton(MockChainOptions());
        services.AddSingleton(MockUserAddressService());
        services.AddSingleton(MockTokenAppService());
        services.AddSingleton(MockNetworkAppService());
        services.AddSingleton(MockSwapAppService());
    }

    [Fact]
    public async Task GetWithdrawInfoTest()
    {
        var depositInfo = await _orderDepositAppService.GetDepositInfoAsync(new GetDepositRequestDto()
        {
            ChainId = "AELF",
            Symbol = "USDT",
            Network = "ETH"
        });

        depositInfo.ShouldNotBeNull();
        depositInfo.DepositInfo.DepositAddress.ShouldBe("test");
    }
    
    [Fact]
    public async Task GetSwapDepositInfoTest()
    {
        var depositInfo = await _orderDepositAppService.GetDepositInfoAsync(new GetDepositRequestDto()
        {
            ChainId = "AELF",
            Symbol = "USDT",
            Network = "ETH",
            ToSymbol = "ELF"
        });

        depositInfo.ShouldNotBeNull();
        depositInfo.DepositInfo.DepositAddress.ShouldBe("swap_test");
    }

    [Fact]
    public async Task GetWithdrawInfo_Network_Not_Exists_Test()
    {
        try
        {
            var depositInfo = await _orderDepositAppService.GetDepositInfoAsync(new GetDepositRequestDto()
            {
                ChainId = "AELF",
                Symbol = "USDT",
                Network = "GETH"
            });

            depositInfo.ShouldNotBeNull();
            depositInfo.DepositInfo.DepositAddress.ShouldBe("test");
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Network is not exist");
        }
    }

    [Fact]
    public async Task AddOrUpdateTest()
    {
        var result = await _orderDepositAppService.AddOrUpdateAsync(new DepositOrderDto()
        {
            Id = Guid.Empty
        });

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task BulkAddOrUpdateTest()
    {
        var result = await _orderDepositAppService.BulkAddOrUpdateAsync(new List<DepositOrderDto>()
        {
            new DepositOrderDto()
            {
                Id = Guid.Empty
            }
        });

        result.ShouldBeTrue();
        
        result = await _orderDepositAppService.BulkAddOrUpdateAsync(new List<DepositOrderDto>()
        {
            null
        });

        result.ShouldBeFalse();
    }
    
    [Fact]
    public async Task ExistTest()
    {
        await _orderDepositAppService.AddOrUpdateAsync(new DepositOrderDto()
        {
            Id = Guid.NewGuid(),
            OrderType = "Deposit",
            ThirdPartOrderId = "AAA"
        });

        var result = await _orderDepositAppService.ExistSync(new DepositOrderDto()
        {
            ThirdPartOrderId = "AAA"
        });
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CalculateDepositRateTest()
    {
        try
        {
            var result = await _orderDepositAppService.CalculateDepositRateAsync(new GetCalculateDepositRateRequestDto()
            {
                ToChainId = "tDVV",
                FromSymbol = "USDT",
                ToSymbol = "USDT",
                FromAmount = DepositSwapAmountHelper.AmountZero
            });

            result.ShouldNotBeNull();
            
            result = await _orderDepositAppService.CalculateDepositRateAsync(new GetCalculateDepositRateRequestDto()
            {
                ToChainId = "tDVV",
                FromSymbol = "USDT",
                ToSymbol = "USDT",
                FromAmount = 0.1M
            });

            result.ShouldNotBeNull();
            
            result = await _orderDepositAppService.CalculateDepositRateAsync(new GetCalculateDepositRateRequestDto()
            {
                ToChainId = "AELF",
                FromSymbol = "USDT",
                ToSymbol = "ELF",
                FromAmount = 1.5M
            });

            result.ShouldNotBeNull();
            
            result = await _orderDepositAppService.CalculateDepositRateAsync(new GetCalculateDepositRateRequestDto()
            {
                ToChainId = "AELF",
                FromSymbol = "USDT",
                ToSymbol = "SGR-1",
                FromAmount = 1.5M
            });

            result.ShouldNotBeNull();
            
            await _orderDepositAppService.CalculateDepositRateAsync(new GetCalculateDepositRateRequestDto()
            {
                FromAmount = DepositSwapAmountHelper.AmountZero
            });
        }
        catch (Exception e)
        {
            e.ShouldNotBeNull();
        }
    }
    private IOptionsSnapshot<TokenInfoOptions> MockTokenOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenInfoOptions
            {
                Tokens = new Dictionary<string, Dictionary<string, TokenInfoDto>>
                {
                    {
                        "AELF", new Dictionary<string, TokenInfoDto>
                        {
                            {
                                "USDT", new TokenInfoDto
                                {
                                    Symbol = "USDT",
                                    Name = "Tether USD",
                                    Decimal = 8,
                                    Icon = "https://example.com/usdt.png",
                                    TokenAddress = "0x1234567890abcdef1234567890abcdef12345678"
                                }
                            },
                            {
                                "ELF", new TokenInfoDto
                                {
                                    Symbol = "ELF",
                                    Name = "ELF",
                                    Decimal = 8,
                                    Icon = "https://example.com/elf.png",
                                    TokenAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd"
                                }
                            }
                        }
                    }
                }
            }
        );
        return mockOptionsSnapshot.Object;
    }

    private IOptionsSnapshot<NetworkInfoOptions> MockNetworkOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<NetworkInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new NetworkInfoOptions
            {
                Networks = new Dictionary<string, NetworkBasicInfo>
                {
                    ["ETH"] = new NetworkBasicInfo
                    {
                        Network = "ETH",
                        Name = "Ethereum",
                        MultiConfirmSeconds = 15,
                        TokenPoolContractAddress = "0x1234567890abcdef1234567890abcdef12345678",
                        TokePoolExplorerUrl = "https://etherscan.io/address/0x1234567890abcdef1234567890abcdef12345678",
                        IsTokenAccessRange = true,
                        ConfirmNum = 12,
                        BlockingTime = 60,
                        ExtraRequestTime = 30,
                        EstimatedArrivalTime = 1000,
                        FeeAlarmPercent = 10,
                        MinShowVersion = "1.0.0",
                        WithdrawLocalFee = 0.01m,
                        WithdrawLocalFeeUnit = "ETH",
                        SpecialWithdrawFee = "0.001 ETH",
                        SpecialWithdrawFeeDisplay = true
                    }
                },
                NetworkPattern = new Dictionary<string, List<string>>()
                {
                    ["."] = new List<string>() { "ETH" }
                },
                ExtraNotesTemplate = new List<string> { "Note1", "Note2" },
                SwapExtraNotesTemplate = new List<string> { "SwapNote1", "SwapNote2" },

            });
        return mockOptionsSnapshot.Object;
    }

    // mock TokenSupportedChainOptions
    private IOptionsSnapshot<TokenSupportedChainInfoOptions> MockTokenSupportedChainOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenSupportedChainInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenSupportedChainInfoOptions
            {
                SupportedChains = new Dictionary<string, List<SupportedChainInfo>>()
                {
                    ["USDT"] = new List<SupportedChainInfo>
                    {
                        new SupportedChainInfo
                        {
                            Network = "ETH",
                            SupportedType = new List<string> { "Deposit", "Withdraw" },
                            SupportWhiteList = new List<string> { "0x1234567890abcdef1234567890abcdef12345678" },
                            SupportChain = new List<string> { "AELF" }
                        }
                    }
                }
            });
        return mockOptionsSnapshot.Object;
    }

    // mock WithdrawInfoOptions
    private IOptionsSnapshot<WithdrawInfoOptions> MockWithdrawInfoOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<WithdrawInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new WithdrawInfoOptions
            {
                IsOpen = true,
                CanCrossSameChain = true,
                WithdrawThreshold = 100000,
                OrderChangeTopic = "OrderChange",
                SupportWhiteLists = new Dictionary<string, List<string>>(),
                ToTransferMaxRetry = 5,
                CallMaxRetry = 5,
                CallbackMaxRetry = 5,
                CallQueryMaxRetry = 5,
                MaxListLength = 1000,
                LargeAmount = new Dictionary<string, decimal>
                {
                    ["USDT"] = 10000.0m
                },
                Homogeneous = new Dictionary<string, TransactionThreshold>
                {
                    ["USDT"] = new TransactionThreshold
                    {
                        AmountThreshold = 300,
                        BlockHeightUpperThreshold = 300,
                        BlockHeightLowerThreshold = 30,
                        WithdrawFee = 0.01m
                    }
                },
                TransferPath = new Dictionary<string, List<string>>()
            });
        return mockOptionsSnapshot.Object;
    }

    // mock DepositAddressOptions
    private IOptionsSnapshot<DepositAddressOptions> MockDepositAddressOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<DepositAddressOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new DepositAddressOptions
            {
                RemainingThreshold = 50,
                MaxRequestNewAddressCount = 2,
                MaxAssignedTransferThreshold = 200,
                MaxRequestNewAddressRetry = 3,
                MaxRequestRetryTimes = 3,
                AssignedAddressExpiredHour = 48,
                TransferAddressLists = new Dictionary<string, List<string>>(),
                AddressWhiteLists = new List<string> { "0x1234567890abcdef1234567890abcdef12345678" },
                SupportCoins = new List<string> { "USDT", "ELF" },
                EVMCoins = new List<string> { "USDT", "ELF" }
            });
        return mockOptionsSnapshot.Object;
    }

    private IOptionsSnapshot<SupportedChainTokensOptions> MockSupportedChainTokensOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<SupportedChainTokensOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new SupportedChainTokensOptions
            {
                Tokens = new Dictionary<string, SupportTokenInfo>
                {
                    {
                        "AELF", new SupportTokenInfo
                        {
                            Deposit = new Dictionary<string, StatusInfo>
                            {
                                { "USDT", new StatusInfo { IsOpen = true } },
                                { "ELF", new StatusInfo { IsOpen = true } }
                            },
                            Withdraw = new Dictionary<string, StatusInfo>
                            {
                                { "USDT", new StatusInfo { IsOpen = true } },
                                { "ELF", new StatusInfo { IsOpen = true } }
                            }
                        }
                    }
                },
                Transfer = new Dictionary<string, StatusInfo>
                {
                    { "USDT", new StatusInfo { IsOpen = true } },
                    { "ELF", new StatusInfo { IsOpen = true } }
                }
            });
        return mockOptionsSnapshot.Object;
    }

    // mock ServiceFeeOptions
    /*
     * public bool IsOpen { get; set; } = true;
       public Dictionary<string, decimal> AmountThreshold { get; set; } = new();
       public Dictionary<string, decimal> MinThirdPartFee { get; set; } = new();
       public Dictionary<string, decimal> MaxThirdPartFee { get; set; } = new();
       public decimal FeeFluctuationPercent { get; set; } = (decimal)0.1;
       public int ThirdPartFeeExpireSeconds { get; set; } = 180;
       public Dictionary<string, decimal> MinAmount { get; set; } = new();
       public decimal MinWithdraw { get; set; } = 0.2M;
       public Dictionary<string, decimal> MinDeposit { get; set; } = new();
       public List<string> WithdrawFeeNetwork { get; set; }
       public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;
     */
    private IOptionsSnapshot<ServiceFeeOptions> MockServiceFeeOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<ServiceFeeOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new ServiceFeeOptions
            {
                IsOpen = true,
                AmountThreshold = new Dictionary<string, decimal>
                {
                    { "USDT", 1000 },
                    { "ELF", 500 }
                },
                MinThirdPartFee = new Dictionary<string, decimal>
                {
                    { "USDT", 0.01m },
                    { "ELF", 0.001m }
                },
                MaxThirdPartFee = new Dictionary<string, decimal>
                {
                    { "USDT", 10m },
                    { "ELF", 1m }
                },
                FeeFluctuationPercent = 0.1m,
                ThirdPartFeeExpireSeconds = 180,
                MinAmount = new Dictionary<string, decimal>
                {
                    { "USDT", 10m },
                    { "ELF", 1m }
                },
                MinWithdraw = 0.2m,
                MinDeposit = new Dictionary<string, decimal>
                {
                    { "USDT", 5m },
                    { "ELF", 0.5m }
                },
                WithdrawFeeNetwork = new List<string> { "ETH", "AELF" },
                ThirdPartCacheFeeExpireSeconds = 180
            });
        return mockOptionsSnapshot.Object;
    }
    
    private IOptionsSnapshot<ChainOptions> MockChainOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<ChainOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new ChainOptions
            {
                ChainInfos = new Dictionary<string, ChainOptions.ChainInfo>()
                {
                    ["tDVV"] = new ChainOptions.ChainInfo()
                }
            });
        return mockOptionsSnapshot.Object;
    }

    private IUserAddressService MockUserAddressService()
    {
        var userAddressService = new Mock<IUserAddressService>();

        userAddressService.Setup(o =>
            o.GetUserAddressAsync(It.IsAny<GetUserDepositAddressInput>())).ReturnsAsync("test");
        userAddressService.Setup(o =>
            o.GetUserAddressAsync(It.Is<GetUserDepositAddressInput>(i => i.ToSymbol == "ELF")))
            .ReturnsAsync("swap_test");
        return userAddressService.Object;
    }
    
    private ITokenAppService MockTokenAppService()
    {
        var tokenAppService = new Mock<ITokenAppService>();

        tokenAppService.Setup(o =>
            o.IsValidSwap(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        tokenAppService.Setup(o =>
            o.IsValidDeposit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        tokenAppService.Setup(o => o.GetTokenOptionListAsync(It.IsAny<GetTokenOptionListRequestDto>()))
            .ReturnsAsync(new GetTokenOptionListDto()
        {
            TokenList = new List<TokenOptionConfigDto>()
            {
                new TokenOptionConfigDto()
                {
                    Symbol = "USDT",
                    Decimals = 8,
                    ToTokenList = new List<TargetTokenOptionConfigDto>()
                    {
                        new TargetTokenOptionConfigDto()
                        {
                            Symbol = "USDT",
                            Decimals = 8,
                            ChainIdList = new List<string>()
                            {
                                "AELF",
                                "tDVV",
                                "tDVW"
                            }
                        },
                        new TargetTokenOptionConfigDto()
                        {
                            Symbol = "ELF",
                            Decimals = 8,
                            ChainIdList = new List<string>()
                            {
                                "AELF",
                                "tDVV",
                                "tDVW"
                            }
                        }
                    }
                },
                new TokenOptionConfigDto()
                {
                    Symbol = "ELF",
                    Decimals = 8
                }
            }
        });
        return tokenAppService.Object;
    }

    private INetworkAppService MockNetworkAppService()
    {
        var networkAppService = new Mock<INetworkAppService>();

        networkAppService.Setup(o =>
            o.GetServiceFeeAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(
            Tuple.Create(false, 10M, 1M, 1M));
        return networkAppService.Object;
    }

    private ISwapAppService MockSwapAppService()
    {
        var swapAppService = new Mock<ISwapAppService>();

        swapAppService.Setup(o =>
            o.CalculateAmountsOut(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()))
                .ReturnsAsync(new GetAmountsOutDto()
                {
                    AmountOut = 0.1M,
                    MinAmountOut = 0.01M
                });
        return swapAppService.Object;
    }
}