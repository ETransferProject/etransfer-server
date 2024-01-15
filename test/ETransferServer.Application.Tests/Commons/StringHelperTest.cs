using System;
using AElf.Types;
using FluentAssertions;
using Shouldly;
using ETransferServer.Common;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Commons;

public class StringHelperTest : ETransferServerApplicationTestBase
{


    public StringHelperTest(ITestOutputHelper output) : base(output)
    {
    }
    
    
    [Fact]
    public void IsAddressTest()
    {
        "aaa".TryParseBase58Address(out _).ShouldBe(false);
        ((string)null).TryParseBase58Address(out _).ShouldBe(false);
        "".TryParseBase58Address(out _).ShouldBe(false);
        "2jxnT1HxRr9PrfMjrhEvKyKABDa82oxhQVyrbnw7VkosKoBqvE".TryParseBase58Address(out var add).ShouldBe(true);
        add.ToBase58().ShouldBe("2jxnT1HxRr9PrfMjrhEvKyKABDa82oxhQVyrbnw7VkosKoBqvE");

    }

}