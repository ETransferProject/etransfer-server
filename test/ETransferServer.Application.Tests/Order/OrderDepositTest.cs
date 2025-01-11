using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
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

    private IOptionsSnapshot<NetworkOptions> MockNetworkOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<NetworkOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new NetworkOptions
            {
                NetworkMap = new Dictionary<string, List<NetworkConfig>>()
                {
                    ["USDT"] = new List<NetworkConfig>()
                    {
                        new NetworkConfig()
                        {
                            NetworkInfo = new NetworkInfo()
                            {
                                Network = "ETH"
                            },
                            DepositInfo = new DepositInfo()
                            {
                                MinDeposit = "1",
                                ExtraNotes = new List<string>() { "test" }
                            }
                        }
                    },
                    ["ELF"] = new List<NetworkConfig>()
                    {
                        new NetworkConfig()
                        {
                            NetworkInfo = new NetworkInfo()
                            {
                                Network = "AELF"
                            },
                            DepositInfo = new DepositInfo()
                            {
                                MinDeposit = "1",
                                ExtraNotes = new List<string>() { "test" }
                            }
                        }
                    }
                }
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
                    ToTokenList = new List<ToTokenOptionConfigDto>()
                    {
                        new ToTokenOptionConfigDto()
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
                        new ToTokenOptionConfigDto()
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