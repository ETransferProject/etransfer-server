using ETransferServer.Grains.Options;
using Microsoft.Extensions.Options;
using Moq;
using Orleans.TestingHost;
using Xunit.Abstractions;

namespace ETransferServer.Grain.Test;

public class ETransferServerTestBase : ETransferServerTestBase<ETransferServerGrainTestModule>
{
    protected readonly TestCluster Cluster;
    public ETransferServerTestBase(ITestOutputHelper output) : base(output)
    {
        Cluster = GetRequiredService<ClusterFixture>().Cluster;
    }


    public IOptionsMonitor<TimerOptions> MockTimerOption()
    {

        var option = new TimerOptions
        {
            WithdrawTimer = new(60, 60)
        };

        var mockOption = new Mock<IOptionsMonitor<TimerOptions>>();
        mockOption.Setup(p => p.CurrentValue).Returns(option);
        return mockOption.Object;
    }

}