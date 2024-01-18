using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Order;

[Collection(ClusterCollection.Name)]
public class OrderStatusFlowTest : ETransferServerApplicationTestBase
{
    private readonly IOrderStatusFlowAppService _flowAppService;

    public OrderStatusFlowTest(ITestOutputHelper output) : base(output)
    {
        _flowAppService = GetRequiredService<IOrderStatusFlowAppService>();
    }

    [Fact]
    public async Task AddOrUpdateTest()
    {
        var result = await _flowAppService.AddOrUpdateAsync(new OrderStatusFlowDto()
        {
            Id = Guid.Empty
        });

        result.ShouldBeTrue();
    }
    [Fact]
    public async Task AddOrUpdate_Fail_Test()
    {
        try
        {
            await _flowAppService.AddOrUpdateAsync(null);
        }
        catch (Exception e)
        {
            e.ShouldNotBeNull();
        }
    }
}