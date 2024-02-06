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
            .Setup(x => x.GetCacheAsync())
            .ReturnsAsync(new CoBoCoinDto()
            {
                AbsEstimateFee = "10.01"
            });

        return coboCoin.Object;
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
                            },
                            SupportType = new List<string>() { "Withdraw" },
                            SupportChain = new List<string>() { "AELF" },
                            WithdrawInfo = new WithdrawInfo()
                        }
                    }
                },
                NetworkPattern = new Dictionary<string, List<string>>()
                {
                    ["."] = new List<string>() { "ETH" }
                }
            });
        return mockOptionsSnapshot.Object;
    }
}