using Xunit.Abstractions;

namespace ETransferServer;

public abstract class ETransferServerDomainTestBase : ETransferServerTestBase<ETransferServerDomainTestModule>
{
    protected ETransferServerDomainTestBase(ITestOutputHelper output) : base(output)
    {
    }
}