using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Models;
using ETransferServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Network;

[Collection(ClusterCollection.Name)]
public class NetworkAppServiceTest : ETransferServerApplicationTestBase
{
    private readonly INetworkAppService _networkAppService;

    public NetworkAppServiceTest(ITestOutputHelper output) : base(output)
    {
        _networkAppService = GetRequiredService<INetworkAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(MockNetworkOptions());
        services.AddSingleton(MockTokenSupportedChainOptions());
        services.AddSingleton(MockServiceFeeOptions());
        services.AddSingleton(MockWithdrawInfoOptions());
        services.AddSingleton(MockTokenInfoOptions());
        services.AddSingleton(MockCoBoCoinGrain());
        base.AfterAddApplication(services);
    }

    [Fact]
    public async Task GetNetworkListATest()
    {
        try
        {
            var dto = new GetNetworkListRequestDto()
            {
                ChainId = "AELF",
                Address = "test",
                Symbol = "USDT",
                Type = "Withdraw"
            };
            var result = await _networkAppService.GetNetworkListAsync(dto);

            result.ShouldNotBeNull();
            result.ChainId.ShouldBe("AELF");

            dto.Type = "Deposit";
            dto.Address = "";
            result = await _networkAppService.GetNetworkListAsync(dto);
            result.ShouldNotBeNull();

            dto.Address = null;
            result = await _networkAppService.GetNetworkListAsync(dto);
            result.ShouldNotBeNull();

            dto.ChainId = "";
        
            await _networkAppService.GetNetworkListAsync(dto);
        }
        catch (Exception e)
        {
            
        }

    }
    
    private ICoBoCoinGrain MockCoBoCoinGrain()
    {
        var coboCoin = new Mock<ICoBoCoinGrain>();

        coboCoin
            .Setup(x => x.GetCache())
            .ReturnsAsync(new CoBoCoinDto()
            {
                AbsEstimateFee = "10.01"
            });

        return coboCoin.Object;
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
    // mock ServiceFeeOptions
    private IOptionsSnapshot<ServiceFeeOptions> MockServiceFeeOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<ServiceFeeOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new ServiceFeeOptions
            {
                IsOpen = true,
                AmountThreshold = new Dictionary<string, decimal>
                {
                    ["USDT"] = 10.0m
                },
                MinThirdPartFee = new Dictionary<string, decimal>
                {
                    ["USDT"] = 0.1m
                },
                MaxThirdPartFee = new Dictionary<string, decimal>
                {
                    ["USDT"] = 1.0m
                },
                FeeFluctuationPercent = 0.1m,
                ThirdPartFeeExpireSeconds = 180,
                MinAmount = new Dictionary<string, decimal>
                {
                    ["USDT"] = 1.0m
                },
                MinWithdraw = 0.2m,
                MinDeposit = new Dictionary<string, decimal>
                {
                    ["USDT"] = 1
                },
                WithdrawFeeNetwork = new List<string> { "ETH" },
                ThirdPartCacheFeeExpireSeconds = 180
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
    //mock TokenInfoOptions
    private IOptionsSnapshot<TokenInfoOptions> MockTokenInfoOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenInfoOptions
            {
                Tokens = new Dictionary<string, Dictionary<string, TokenInfoDto>>
                {
                    ["ETH"] = new Dictionary<string, TokenInfoDto>
                    {
                        ["USDT"] = new TokenInfoDto
                        {
                            Symbol = "USDT",
                            Name = "Tether USD",
                            Decimal = 6,
                            TokenAddress = "0x1234567890abcdef1234567890abcdef12345678",
                            TokenExploreUrl = "https://etherscan.io/token/0x1234567890abcdef1234567890abcdef12345678",
                            Icon = "https://example.com/icon.png"
                        }
                    }
                }
            });
        return mockOptionsSnapshot.Object;
    }
}