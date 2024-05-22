using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Users;
using Xunit;
using Xunit.Abstractions;

namespace ETransferServer.Order;

[Collection(ClusterCollection.Name)]
public class OrderAppServiceTest : ETransferServerApplicationTestBase
{
    protected ICurrentUser _currentUser;
    private readonly IOrderAppService _orderAppService;
    private readonly IOrderDepositAppService _orderDepositAppService;
    private readonly IOrderWithdrawAppService _orderWithdrawAppService;

    public OrderAppServiceTest(ITestOutputHelper output) : base(output)
    {
        _orderAppService = GetRequiredService<IOrderAppService>();
        _orderDepositAppService = GetRequiredService<IOrderDepositAppService>();
        _orderWithdrawAppService = GetRequiredService<IOrderWithdrawAppService>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        base.AfterAddApplication(services);
        _currentUser = Substitute.For<ICurrentUser>();
        services.AddSingleton(_currentUser);
    }

    private void Login(Guid userId)
    {
        _currentUser.Id.Returns(userId);
        _currentUser.IsAuthenticated.Returns(true);
    }

    [Fact]
    public async Task GetOrderRecordListAsyncTest()
    {
        var input = new GetOrderRecordRequestDto()
        {
            Type = 0,
            Status = 0
        };
        var result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBe(0);

        var status = await _orderAppService.GetOrderRecordStatusAsync();
        status.Status.ShouldBeFalse();

        await _orderDepositAppService.AddOrUpdateAsync(new DepositOrderDto()
        {
            Id = Guid.Empty,
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Deposit",
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                ToAddress = "AA",
                Amount = 10,
                Status = "Confirmed"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                ToAddress = "BB",
                Amount = 9,
                Status = "success"
            },
            Status = "Finish",
            CreateTime = DateTime.UtcNow.AddHours(-2).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds()
        });

        var withdrawOrderDto = new WithdrawOrderDto()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Withdraw",
            FromTransfer = new TransferInfo
            {
                Network = "AELF",
                ChainId = "AELF",
                ToAddress = "CC",
                Amount = 20,
                Status = "Confirmed"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ToAddress = "DD",
                Amount = 19,
                Status = "success",
                FeeInfo = new List<FeeInfo>
                {
                    new FeeInfo()
                    {
                        Amount = "1",
                        Symbol = "USDT"
                    }
                }
            },
            Status = "Finish",
            CreateTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.ToUtcMilliSeconds()
        };
        await _orderWithdrawAppService.AddOrUpdateAsync(withdrawOrderDto);
        withdrawOrderDto.Id = Guid.Parse("20000000-0000-0000-0000-000000000000");
        withdrawOrderDto.Status = "Pending";
        await _orderWithdrawAppService.AddOrUpdateAsync(withdrawOrderDto);
        withdrawOrderDto.Id = Guid.Parse("30000000-0000-0000-0000-000000000000");
        withdrawOrderDto.Status = "Failed";
        await _orderWithdrawAppService.AddOrUpdateAsync(withdrawOrderDto);

        Login(Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"));

        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.Type = 2;
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.Status = 1;
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);
        input.Status = 2;
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);
        input.Status = 3;
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.Type = 0;
        input.Status = 0;
        input.StartTimestamp = DateTime.UtcNow.AddMinutes(-1).ToUtcMilliSeconds();
        input.EndTimestamp = DateTime.UtcNow.AddMinutes(1).ToUtcMilliSeconds();
        input.SkipCount = 0;
        input.MaxResultCount = 20;
        input.Sorting = "arrivalTime";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.Sorting = "arrivalTime asc";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        status = await _orderAppService.GetOrderRecordStatusAsync();
        // status.Status.ShouldBeTrue();
        status.Status.ShouldBeFalse();

        input.Sorting = " ";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);
    }
}