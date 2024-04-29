using System;
using System.Threading.Tasks;
using ETransferServer.Common;
using FluentAssertions.Common;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Commons;

public class DateTimeTest : ETransferServerApplicationTestBase
{
    public DateTimeTest(ITestOutputHelper output) : base(output)
    {
    }


    [Fact]
    public async Task LongTest()
    {
        Output.WriteLine(DateTime.UtcNow.ToUtcMilliSeconds().ToString());
    }
    
}