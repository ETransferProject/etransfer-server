using ETransferServer.Common;
using ETransferServer.Grains.Grain.Timers;
using Shouldly;
using Xunit.Abstractions;

namespace ETransferServer.Grain.Test.Timers;

[Collection(ClusterCollection.Name)]
public class WithdrawTimerGrainTest : ETransferServerTestBase
{
    public WithdrawTimerGrainTest(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task GetLastCallBackTimeTest()
    {
        var grain = Cluster.Client.GetGrain<IWithdrawTimerGrain>(GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
        var time = await grain.GetLastCallBackTime();
        time.Date.Day.ShouldBe(DateTime.UtcNow.Day);
    }
}