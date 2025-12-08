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


    public IOptionsSnapshot<TimerOptions> MockTimerOption()
    {

        var option = new TimerOptions
        {
            WithdrawTimer = new(60, 60)
        };

        var mockOption = new Mock<IOptionsSnapshot<TimerOptions>>();
        mockOption.Setup(p => p.Value).Returns(option);
        return mockOption.Object;
    }

}