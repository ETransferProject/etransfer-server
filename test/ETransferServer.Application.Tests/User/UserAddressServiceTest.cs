using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Grain;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Volo.Abp.Users;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.User;

[Collection(ClusterCollection.Name)]
public class UserAddressServiceTest : ETransferServerApplicationTestBase
{
    private readonly IUserAddressService _userAddressService;
    private ICurrentUser _currentUser;

    public UserAddressServiceTest(ITestOutputHelper output) : base(output)
    {
        _userAddressService = GetRequiredService<IUserAddressService>();
        _currentUser = new CurrentUser(new FakeCurrentPrincipalAccessor());
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(MockDepositAddressOptions());
        services.AddSingleton(MockUserDepositAddressGrain());
        services.AddSingleton(MockUserGrain());
        base.AfterAddApplication(services);
    }

    [Fact]
    public async Task UserAddressAsyncTest()
    {
        var list = new List<UserAddressDto>();
        var dto1 = new UserAddressDto()
        {
            UserToken = new TokenDto()
            {
                ChainId = "ETH",
                Symbol = "USDT",
                Address = "Test1"
            },
            IsAssigned = false
        };
        var dto3 = new UserAddressDto();
        dto3 = null;
        list.Add(dto3);
        var result = await _userAddressService.BulkAddOrUpdateAsync(list);
        result.ShouldBeFalse();
        list.Remove(dto3);
        list.Add(dto1);
        result = await _userAddressService.BulkAddOrUpdateAsync(list);
        result.ShouldBeTrue();
        var dto2 = new UserAddressDto()
        {
            UserToken = new TokenDto()
            {
                ChainId = "ETH",
                Symbol = "USDT",
                Address = "Test2"
            },
            IsAssigned = true,
            ChainId = "AELF",
            UserId = _currentUser.Id.ToString()
        };
        result = await _userAddressService.AddOrUpdateAsync(dto2);
        result.ShouldBeTrue();

        var input = new GetUserDepositAddressInput()
        {
            ChainId = "AELF",
            NetWork = "ETH",
            Symbol = "USDT"
        };
        var addressDto = await _userAddressService.GetUnAssignedAddressAsync(input);
        addressDto.ShouldBeNull();
        input.NetWork = "SETH";
        addressDto = await _userAddressService.GetUnAssignedAddressAsync(input);
        addressDto.ShouldBeNull();
        var list1 = await _userAddressService.GetRemainingAddressListAsync();
        list1.Count.ShouldBe(2);
        var address = await _userAddressService.GetAssignedAddressAsync("Test2");
        address.ChainId.ShouldBe("AELF");
        address = await _userAddressService.GetAssignedAddressAsync("");
        address.ShouldBeNull();
        var inputList = new List<string>();
        var list2 = await _userAddressService.GetAddressListAsync(inputList);
        list2.ShouldBeNull();
        inputList.Add("Test2");
        list2 = await _userAddressService.GetAddressListAsync(inputList);
        list2.Count.ShouldBe(1);
        
        var dto4 = new UserAddressDto()
        {
            Id = Guid.NewGuid(),
            UserToken = new TokenDto()
            {
                ChainId = "ETH",
                Symbol = "USDT",
                Address = "Test4"
            },
            IsAssigned = true,
            ChainId = "tDVV",
            UpdateTime = DateTime.UtcNow.AddDays(-3).ToUtcMilliSeconds()
        };
        result = await _userAddressService.AddOrUpdateAsync(dto4);
        var list4 = await _userAddressService.GetExpiredAddressListAsync(48);
        list4.Count.ShouldBeGreaterThan(0);

        try
        {
            await _userAddressService.GetUserAddressAsync(input);
        }
        catch (Exception ex)
        {
        }
    }

    private IOptionsSnapshot<DepositAddressOptions> MockDepositAddressOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<DepositAddressOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new DepositAddressOptions
            {
                RemainingThreshold = 1,
                MaxRequestNewAddressCount = 1,
                SupportCoins = new List<string>(){ "SETH_USDT","ETH_USDT" },
                EVMCoins = new List<string>(){ "ETH_USDT" }
            });
        return mockOptionsSnapshot.Object;
    }

    private IUserDepositAddressGrain MockUserDepositAddressGrain()
    {
        var mockUserDepositAddressGrain = new Mock<IUserDepositAddressGrain>();
        mockUserDepositAddressGrain.Setup(o => o.GetUserAddress(It.IsAny<GetUserDepositAddressInput>()))
            .ReturnsAsync(
                new string("test")
            );
        return mockUserDepositAddressGrain.Object;
    }
    
    private IUserGrain MockUserGrain()
    {
        var mockUserGrain = new Mock<IUserGrain>();
        mockUserGrain.Setup(o => o.GetUser())
            .ReturnsAsync(
                new GrainResultDto<UserGrainDto>{
                    Success = true,
                    Data = new UserGrainDto
                    {
                        AppId = WalletEnum.Portkey.ToString(),
                        UserId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d")
                    }}
            );
        return mockUserGrain.Object;
    }
}