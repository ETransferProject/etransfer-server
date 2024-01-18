using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Order;

[Collection(ClusterCollection.Name)]
public class CoBoProviderTest : ETransferServerApplicationTestBase
{
    private readonly ICoBoProvider _coBoProvider;

    public CoBoProviderTest(ITestOutputHelper output) : base(output)
    {
        _coBoProvider = GetRequiredService<ICoBoProvider>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        services.AddSingleton(MockNetWorkReflection());
        services.AddSingleton(MockProxyCoBoClientProvider());
    }

    [Fact]
    public async Task GetAddressesTest()
    {
        var result = await _coBoProvider.GetAddressesAsync("aaa", 10);

        result.ShouldNotBeNull();
        result.Coin.ShouldBe("aaa");
    }

    private IProxyCoBoClientProvider MockProxyCoBoClientProvider()
    {
        var provider = new Mock<IProxyCoBoClientProvider>();
        provider.Setup(t => t.PostAsync<AddressesDto>(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(new AddressesDto()
            {
                Coin = "bbb"
            });

        return provider.Object;
    }

    private IOptionsSnapshot<NetWorkReflectionOptions> MockNetWorkReflection()
    {
        var option = new Mock<IOptionsSnapshot<NetWorkReflectionOptions>>();
        option.Setup(t => t.Value).Returns(new NetWorkReflectionOptions()
        {
            ReflectionItems = new Dictionary<string, string>()
            {
                ["aaa"] = "bbb",
                ["ccc"] = "ddd"
            }
        });
        return option.Object;
    }
}