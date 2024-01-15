using Xunit.Abstractions;

namespace ETransferServer;

public abstract partial class ETransferServerApplicationTestBase : ETransferServerOrleansTestBase<ETransferServerApplicationTestModule>
{

    public  readonly ITestOutputHelper Output;
    protected ETransferServerApplicationTestBase(ITestOutputHelper output) : base(output)
    {
        Output = output;
    }
}