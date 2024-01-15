using System.Collections.Generic;
using System.Threading.Tasks;
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
        base.AfterAddApplication(services);
    }

    [Fact]
    public async Task GetNetworkListATest()
    {
        var result = await _networkAppService.GetNetworkListAsync(new GetNetworkListRequestDto()
        {
            ChainId = "AELF",
            Address = "test",
            Symbol = "USDT",
            Type = "Withdraw"
        });

        result.ShouldNotBeNull();
        result.ChainId.ShouldBe("AELF");
    }

    private IOptions<NetworkOptions> MockNetworkOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptions<NetworkOptions>>();
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