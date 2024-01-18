using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using ETransferServer.Options;
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
        services.AddSingleton(MockSignatureServerOptions());
        services.AddSingleton(MockUserAddressService());
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


    private IOptionsSnapshot<NetworkOptions> MockSignatureServerOptions()
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
                    }
                }
            });
        return mockOptionsSnapshot.Object;
    }

    private IUserAddressService MockUserAddressService()
    {
        var userAddressService = new Mock<IUserAddressService>();

        userAddressService.Setup(o =>
            o.GetUserAddressAsync(It.IsAny<GetUserDepositAddressInput>())).ReturnsAsync("test");
        return userAddressService.Object;
    }
}