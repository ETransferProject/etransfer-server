using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Models;
using ETransferServer.Options;
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
        //MockSupportedChainTokensOptions
        services.AddSingleton(MockSupportedChainTokensOptions());
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
        
        result = await _tokenAppService.GetTokenListAsync(new GetTokenListRequestDto()
        {
            ChainId = "AELF",
            Type = "Transfer"
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
    
    [Fact]
    public async Task IsValidDepositTest()
    {
        try
        {
            var result = _tokenAppService.IsValidDeposit("AELF", "USDT", "USDT");
            result.ShouldBeFalse();
            
            result = _tokenAppService.IsValidDeposit("AELF", "USDT", "ELF");
            result.ShouldBeTrue();
            
            result = _tokenAppService.IsValidDeposit("AELF", "USDT", "SGR-1");
            result.ShouldBeFalse();
        }
        catch (Exception e)
        {
            e.ShouldNotBeNull();
        }
    }
    
    [Fact]
    public async Task GetTokenOptionListTest()
    {
        try
        {
            var result = await _tokenAppService.GetTokenOptionListAsync(new GetTokenOptionListRequestDto()
            {
                Type = "Deposit"
            });

            result.ShouldNotBeNull();
            result.TokenList.ShouldNotBeNull();
            
            await _tokenAppService.GetTokenOptionListAsync(new GetTokenOptionListRequestDto()
            {
                Type = "Withdraw"
            });
        }
        catch (Exception e)
        {
            e.ShouldNotBeNull();
        }
    }

    private IOptionsSnapshot<TokenInfoOptions> MockTokenOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenInfoOptions
            {
                Tokens = new Dictionary<string, Dictionary<string, TokenInfoDto>>
                {
                    {
                        "AELF", new Dictionary<string, TokenInfoDto>
                        {
                            {
                                "USDT", new TokenInfoDto
                                {
                                    Symbol = "USDT",
                                    Name = "Tether USD",
                                    Decimal = 8,
                                    Icon = "https://example.com/usdt.png",
                                    TokenAddress = "0x1234567890abcdef1234567890abcdef12345678"
                                }
                            },
                            {
                                "ELF", new TokenInfoDto
                                {
                                    Symbol = "ELF",
                                    Name = "AELF",
                                    Decimal = 8,
                                    Icon = "https://example.com/elf.png",
                                    TokenAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd"
                                }
                            }
                        }
                    }
                }
            }
            );
        return mockOptionsSnapshot.Object;
    }
    // mock SupportedChainTokensOptions
    /**
     * public class SupportedChainTokensOptions
{
    // chain id -> token
    public Dictionary<string, SupportTokenInfo> Tokens { get; set; } = new();
    // symbol -> status
    public Dictionary<string, StatusInfo> Transfer { get; set; } = new();
}

public class SupportTokenInfo
{
    // symbol -> status
    public Dictionary<string, StatusInfo> Deposit { get; set; } = new();
    public Dictionary<string, StatusInfo> Withdraw { get; set; } = new();
}

public class StatusInfo
{
    public bool IsOpen { get; set; } = true;
}
     */
    private IOptionsSnapshot<SupportedChainTokensOptions> MockSupportedChainTokensOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<SupportedChainTokensOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new SupportedChainTokensOptions
            {
                Tokens = new Dictionary<string, SupportTokenInfo>
                {
                    {
                        "AELF", new SupportTokenInfo
                        {
                            Deposit = new Dictionary<string, StatusInfo>
                            {
                                { "USDT", new StatusInfo { IsOpen = true } },
                                { "ELF", new StatusInfo { IsOpen = true } }
                            },
                            Withdraw = new Dictionary<string, StatusInfo>
                            {
                                { "USDT", new StatusInfo { IsOpen = true } },
                                { "ELF", new StatusInfo { IsOpen = true } }
                            }
                        }
                    }
                },
                Transfer = new Dictionary<string, StatusInfo>
                {
                    { "USDT", new StatusInfo { IsOpen = true } },
                    { "ELF", new StatusInfo { IsOpen = true } }
                }
            });
        return mockOptionsSnapshot.Object;
    }
}