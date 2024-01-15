using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Models;
using ETransferServer.Options;
using ETransferServer.token;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Token;

[Collection(ClusterCollection.Name)]
public class TokenAppServiceTest : ETransferServerApplicationTestBase
{
    private readonly ITokenAppService _tokenAppService;

    public TokenAppServiceTest(ITestOutputHelper output) : base(output)
    {
        _tokenAppService = GetRequiredService<ITokenAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(MockTokenOptions());
        base.AfterAddApplication(services);
    }

    [Fact]
    public async Task GetTokenListTest()
    {
        var result = await _tokenAppService.GetTokenListAsync(new GetTokenListRequestDto()
        {
            ChainId = "AELF",
            Type = "Withdraw"
        });

        result.ShouldNotBeNull();
        result.ChainId.ShouldBe("AELF");
    }
    
    [Fact]
    public async Task GetTokenList_Type_Not_Exists_Test()
    {
        try
        {
            var result = await _tokenAppService.GetTokenListAsync(new GetTokenListRequestDto()
            {
                ChainId = "AELF",
                Type = "Deposit"
            });

            result.ShouldNotBeNull();
            result.ChainId.ShouldBe("AELF");
        }
        catch (Exception e)
        {
            e.ShouldNotBeNull();
        }
    }

    private IOptions<TokenOptions> MockTokenOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptions<TokenOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenOptions
            {
                Withdraw = new Dictionary<string, List<TokenConfig>>()
                {
                    ["AELF"] = new List<TokenConfig>()
                    {
                        new TokenConfig()
                        {
                            Symbol = "USDT",
                            Name = "USDT",
                            Decimals = 6
                        }
                    }
                }
            });
        return mockOptionsSnapshot.Object;
    }
}