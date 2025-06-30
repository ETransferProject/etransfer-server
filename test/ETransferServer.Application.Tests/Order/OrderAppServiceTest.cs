using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Entities;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Options;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User;
using ETransferServer.User.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using NSubstitute;
using Shouldly;
using Volo.Abp.Users;
using Xunit;
using Xunit.Abstractions;
using TransactionThreshold = ETransferServer.Options.TransactionThreshold;

namespace ETransferServer.Order;

[Collection(ClusterCollection.Name)]
public class OrderAppServiceTest : ETransferServerApplicationTestBase
{
    protected ICurrentUser _currentUser;
    private readonly IOrderAppService _orderAppService;
    private readonly IOrderDepositAppService _orderDepositAppService;
    private readonly IOrderWithdrawAppService _orderWithdrawAppService;
    private readonly INESTRepository<UserIndex, Guid> _userIndexRepository;

    public OrderAppServiceTest(ITestOutputHelper output) : base(output)
    {
        _orderAppService = GetRequiredService<IOrderAppService>();
        _orderDepositAppService = GetRequiredService<IOrderDepositAppService>();
        _orderWithdrawAppService = GetRequiredService<IOrderWithdrawAppService>();
        _userIndexRepository = GetRequiredService<INESTRepository<UserIndex, Guid>>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.AddSingleton(MockTokenOptions());
        services.AddSingleton(MockCoBoCoinGrain());
        services.AddSingleton(MockUserAppService());
        services.AddSingleton(MockNetworkOptions());
        services.AddSingleton(MockTokenSupportedChainOptions());
        services.AddSingleton(MockSupportedChainTokensOptions());
        services.AddSingleton(MockDepositAddressOptions());
        services.AddSingleton(MockServiceFeeOptions());
        services.AddSingleton(MockWithdrawInfoOptions());
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

        var status = await _orderAppService.GetOrderRecordStatusAsync(new GetOrderRecordStatusRequestDto());
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
                Symbol = "USDT",
                ToAddress = "CC",
                Amount = 20,
                Status = "Confirmed"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
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

        input.Sorting = "createTime";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.Sorting = "createTime asc";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        status = await _orderAppService.GetOrderRecordStatusAsync(new GetOrderRecordStatusRequestDto());
        status.Status.ShouldBeFalse();

        input.Sorting = " ";
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        input.AddressList = new List<string>() { "DD" };
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);

        _currentUser.IsAuthenticated.Returns(false);
        result = await _orderAppService.GetOrderRecordListAsync(input);
        result.TotalCount.ShouldBeGreaterThan(0);
    }


    [Fact]
    public async Task GetTransferOrderAsyncTest()
    {
        await _orderWithdrawAppService.AddOrUpdateAsync(new WithdrawOrderDto()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Withdraw",
            ExtensionInfo = new Dictionary<string, string>()
            {
                ["OrderType"] = "Transfer",
                ["SubStatus"] = "UserTransferRejected"
            },
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                FromAddress = "AA",
                ToAddress = "BB",
                Amount = 10,
                Status = "Confirmed",
                TxId = "0x1"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                FromAddress = "CC",
                ToAddress = "DD",
                Amount = 9,
                Status = "success"
            },
            CreateTime = DateTime.UtcNow.AddHours(-2).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds()
        });

        var orderIndex = await _orderAppService.GetTransferOrderAsync(new CoBoTransactionDto()
        {
            TxId = "0x1"
        });
        orderIndex.Id.ShouldBe(Guid.Parse("10000000-0000-0000-0000-000000000000"));
        orderIndex.OrderType.ShouldBe("Withdraw");

        orderIndex = await _orderAppService.GetTransferOrderAsync(new CoBoTransactionDto()
        {
            Coin = "ETH_USDT",
            SourceAddress = "AA",
            Address = "BB",
            AbsAmount = "10",
            Id = "xxx",
            TxId = "xxx"
        });
        orderIndex.Id.ShouldBe(Guid.Parse("10000000-0000-0000-0000-000000000000"));
        orderIndex.OrderType.ShouldBe("Withdraw");
    }

    [Fact]
    public async Task CheckTransferOrderAsyncTest()
    {
        await _orderWithdrawAppService.AddOrUpdateAsync(new WithdrawOrderDto()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Withdraw",
            ExtensionInfo = new Dictionary<string, string>()
            {
                ["OrderType"] = "Transfer",
                ["SubStatus"] = "UserTransferRejected"
            },
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                FromAddress = "AA",
                ToAddress = "BB",
                Amount = 10,
                Status = "Confirmed",
                TxId = "0x1"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                FromAddress = "CC",
                ToAddress = "DD",
                Amount = 9,
                Status = "success"
            },
            CreateTime = DateTime.UtcNow.AddHours(-2).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds()
        });

        var orderIndex = await _orderAppService.CheckTransferOrderAsync(new CoBoTransactionDto()
        {
            TxId = "0x1"
        }, DateTime.UtcNow.ToUtcMilliSeconds());
        orderIndex.ShouldBe(true);

        orderIndex = await _orderAppService.CheckTransferOrderAsync(new CoBoTransactionDto()
        {
            Coin = "ETH_USDT",
            SourceAddress = "AA",
            Address = "BB",
            AbsAmount = "10",
            Id = "xxx",
            TxId = "xxx"
        }, DateTime.UtcNow.ToUtcMilliSeconds());
        orderIndex.ShouldBe(true);
    }

    [Fact]
    public async Task GetOrderRecordStatusAsyncTest()
    {
        await _orderWithdrawAppService.AddOrUpdateAsync(new WithdrawOrderDto()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Withdraw",
            Status = "Created",
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                FromAddress = "AA",
                ToAddress = "BB",
                Amount = 10,
                Status = "Confirmed",
                TxId = "0x1"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                FromAddress = "CC",
                ToAddress = "DD",
                Amount = 9,
                Status = "success"
            },
            CreateTime = DateTime.UtcNow.AddHours(-2).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds()
        });

        var status = await _orderAppService.GetOrderRecordStatusAsync(new GetOrderRecordStatusRequestDto()
        {
            AddressList = new List<string>()
            {
                "AA"
            }
        });
        status.Status.ShouldBeTrue();

        Login(Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"));

        status = await _orderAppService.GetOrderRecordStatusAsync(new GetOrderRecordStatusRequestDto()
        {
            AddressList = new List<string>()
            {
                "AA"
            }
        });
        status.Status.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserOrderRecordListAsyncByAddressListTest()
    {
        await _userIndexRepository.AddOrUpdateAsync(new UserIndex
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            AddressInfos = new List<UserAddressInfo>()
            {
                new UserAddressInfo()
                {
                    Address = "AA"
                }
            }
        });
        await _orderWithdrawAppService.AddOrUpdateAsync(new WithdrawOrderDto()
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000000"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Withdraw",
            Status = "Created",
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                FromAddress = "AA",
                Amount = 10,
                Status = "Confirmed",
                TxId = "0x1"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                ToAddress = "DD",
                Amount = 9,
                Status = "success"
            },
            CreateTime = DateTime.UtcNow.AddHours(-2).ToUtcMilliSeconds(),
            LastModifyTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds()
        });

        Login(Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"));

        var result = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto()
        {
            Address = "AA",
            AddressList = new List<GetUserAddressDto>()
            {
                new GetUserAddressDto()
                {
                    Address = "AA"
                }
            }
        });
        result.ShouldNotBeNull();
        result.Address.ShouldBe("AA");
        result.Processing.TransferCount.ShouldBe(1);
        result.Processing.WithdrawCount.ShouldBe(1);

        result = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto()
        {
            AddressList = new List<GetUserAddressDto>()
            {
                new GetUserAddressDto()
                {
                    Address = "AA"
                }
            }
        });
        result.ShouldNotBeNull();
        result.AddressList[0].Address.ShouldBe("AA");
        result.Processing.TransferCount.ShouldBe(1);
        result.Processing.WithdrawCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetOrderRecordDetailAsyncTest()
    {
        var input = new DepositOrderDto
        {
            Id = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
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
            ArrivalTime = DateTime.UtcNow.AddHours(-1).ToUtcMilliSeconds(),
            ExtensionInfo = new Dictionary<string, string>()
            {
                [ExtensionKey.FromConfirmingThreshold] = "25",
                [ExtensionKey.FromConfirmedNum] = "30"
            }
        };
        await _orderDepositAppService.AddOrUpdateAsync(input);
        Login(Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"));
        var result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.ExtensionInfo = new Dictionary<string, string>()
        {
            [ExtensionKey.FromConfirmingThreshold] = "0",
            [ExtensionKey.FromConfirmedNum] = "0"
        };
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.OrderType = "Withdraw";
        input.ExtensionInfo = new Dictionary<string, string>()
        {
            [ExtensionKey.FromConfirmingThreshold] = "0",
            [ExtensionKey.FromConfirmedNum] = "0"
        };
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.OrderType = "Deposit";
        input.ExtensionInfo = new Dictionary<string, string>()
        {
            [ExtensionKey.FromConfirmingThreshold] = "30",
            [ExtensionKey.FromConfirmedNum] = "25"
        };
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "FromTransferFailed";
        input.FromTransfer.Status = "Transferring";
        input.ToTransfer.Status = string.Empty;
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "ToTransferFailed";
        input.FromTransfer.Status = "Confirmed";
        input.ToTransfer.Status = "Transferring";
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "Failed";
        input.FromTransfer.Status = "Transferring";
        input.ToTransfer.Status = string.Empty;
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "Failed";
        input.FromTransfer.Status = "Failed";
        input.ToTransfer.Status = string.Empty;
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "FromTransferred";
        input.FromTransfer.Status = "Transferred";
        input.ToTransfer.Status = string.Empty;
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "FromTransferred";
        input.FromTransfer.Status = "StartTransfer";
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "ToTransferred";
        input.FromTransfer.Status = "Confirmed";
        input.ToTransfer.Status = "Transferring";
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();

        input.Status = "ToTransferred";
        input.FromTransfer.Status = "Confirmed";
        input.ToTransfer.Status = "Transferring";
        input.ExtensionInfo = new Dictionary<string, string>()
        {
            [ExtensionKey.ToConfirmedNum] = "1",
            [ExtensionKey.SwapToMain] = Boolean.TrueString,
            [ExtensionKey.SwapOriginFromAddress] = "AA",
            [ExtensionKey.SwapToAddress] = "BB",
            [ExtensionKey.SwapChainId] = "CC"
        };
        await _orderDepositAppService.AddOrUpdateAsync(input);
        result = await _orderAppService.GetOrderRecordDetailAsync("3a946083-ac0e-4e24-b913-3c9fc57ab03b");
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUserOrderRecordListAsyncTest()
    {
        await _userIndexRepository.AddOrUpdateAsync(new UserIndex
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            AddressInfos = new List<UserAddressInfo>()
            {
                new UserAddressInfo()
                {
                    Address = "AA"
                }
            }
        });
        await _orderDepositAppService.AddOrUpdateAsync(new DepositOrderDto()
        {
            Id = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
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

        var input = new GetUserOrderRecordRequestDto()
        {
            Address = "AA"
        };
        var result = await _orderAppService.GetUserOrderRecordListAsync(input);
        result.ShouldNotBeNull();

        var eto = new OrderChangeEto()
        {
            Id = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            UserId = Guid.Parse("3a946083-ac0e-4e24-b913-3c9fc57ab03b"),
            OrderType = "Deposit",
            FromTransfer = new TransferInfo
            {
                Network = "ETH",
                Symbol = "USDT",
                ToAddress = "AA",
                Amount = 10,
                Status = "Transferring"
            },
            ToTransfer = new TransferInfo
            {
                Network = "ETH",
                ChainId = "AELF",
                Symbol = "USDT",
                ToAddress = "BB",
                Amount = 9,
                Status = ""
            },
            Status = "FromTransferFailed",
        };
        result = await _orderAppService.GetUserOrderRecordListAsync(input, eto);
        result.ShouldNotBeNull();

        eto.OrderType = "Withdraw";
        result = await _orderAppService.GetUserOrderRecordListAsync(input, eto);
        result.ShouldNotBeNull();

        input.Time = 48;
        result = await _orderAppService.GetUserOrderRecordListAsync(input);
        result.ShouldNotBeNull();

        input.Address = "BB";
        result = await _orderAppService.GetUserOrderRecordListAsync(input);
        result.ShouldNotBeNull();
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
                                    Name = "ELF",
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

    private ICoBoCoinGrain MockCoBoCoinGrain()
    {
        var coboCoinGrain = new Mock<ICoBoCoinGrain>();

        coboCoinGrain
            .Setup(x => x.GetConfirmingThreshold())
            .ReturnsAsync(50);
        coboCoinGrain
            .Setup(x => x.GetHomogeneousConfirmingThreshold(It.IsAny<decimal>()))
            .ReturnsAsync(30);

        return coboCoinGrain.Object;
    }

    private IUserAppService MockUserAppService()
    {
        var user = new Mock<IUserAppService>();

        user.Setup(t => t.GetUserByAddressAsync(It.IsAny<string>())).ReturnsAsync(
            new UserDto());

        return user.Object;
    }

    private IOptionsSnapshot<NetworkInfoOptions> MockNetworkOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<NetworkInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new NetworkInfoOptions
            {
                Networks = new Dictionary<string, NetworkBasicInfo>
                {
                    ["ETH"] = new NetworkBasicInfo
                    {
                        Network = "ETH",
                        Name = "Ethereum",
                        MultiConfirmSeconds = 15,
                        TokenPoolContractAddress = "0x1234567890abcdef1234567890abcdef12345678",
                        TokePoolExplorerUrl = "https://etherscan.io/address/0x1234567890abcdef1234567890abcdef12345678",
                        IsTokenAccessRange = true,
                        
                        ConfirmNum = 12,
                        BlockingTime = 60,
                        ExtraRequestTime = 30,
                        EstimatedArrivalTime = 1000,
                        FeeAlarmPercent = 10,
                        MinShowVersion = "1.0.0",
                        WithdrawLocalFee = 0.01m,
                        WithdrawLocalFeeUnit = "ETH",
                        SpecialWithdrawFee = "0.001 ETH",
                        SpecialWithdrawFeeDisplay = true
                    }
                },
                NetworkPattern = new Dictionary<string, List<string>>()
                {
                    ["."] = new List<string>() { "ETH" }
                },
                ExtraNotesTemplate = new List<string> { "Note1", "Note2" },
                SwapExtraNotesTemplate = new List<string> { "SwapNote1", "SwapNote2" },
            });
        return mockOptionsSnapshot.Object;
    }

    // mock TokenSupportedChainOptions
    private IOptionsSnapshot<TokenSupportedChainInfoOptions> MockTokenSupportedChainOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenSupportedChainInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new TokenSupportedChainInfoOptions
            {
                SupportedChains = new Dictionary<string, List<SupportedChainInfo>>()
                {
                    ["USDT"] = new List<SupportedChainInfo>
                    {
                        new SupportedChainInfo
                        {
                            Network = "ETH",
                            SupportedType = new List<string> { "Deposit", "Withdraw" },
                            SupportWhiteList = new List<string> { "0x1234567890abcdef1234567890abcdef12345678" },
                            SupportChain = new List<string> { "AELF" }
                        }
                    }
                }
            });
        return mockOptionsSnapshot.Object;
    }

    // mock WithdrawInfoOptions
    private IOptionsSnapshot<WithdrawInfoOptions> MockWithdrawInfoOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<WithdrawInfoOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new WithdrawInfoOptions
            {
                IsOpen = true,
                CanCrossSameChain = true,
                WithdrawThreshold = 100000,
                OrderChangeTopic = "OrderChange",
                SupportWhiteLists = new Dictionary<string, List<string>>(),
                ToTransferMaxRetry = 5,
                CallMaxRetry = 5,
                CallbackMaxRetry = 5,
                CallQueryMaxRetry = 5,
                MaxListLength = 1000,
                LargeAmount = new Dictionary<string, decimal>
                {
                    ["USDT"] = 10000.0m
                },
                Homogeneous = new Dictionary<string, TransactionThreshold>
                {
                    ["USDT"] = new TransactionThreshold
                    {
                        AmountThreshold = 300,
                        BlockHeightUpperThreshold = 300,
                        BlockHeightLowerThreshold = 30,
                        WithdrawFee = 0.01m
                    }
                },
                TransferPath = new Dictionary<string, List<string>>()
            });
        return mockOptionsSnapshot.Object;
    }

    // mock DepositAddressOptions
    private IOptionsSnapshot<DepositAddressOptions> MockDepositAddressOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<DepositAddressOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new DepositAddressOptions
            {
                RemainingThreshold = 50,
                MaxRequestNewAddressCount = 2,
                MaxAssignedTransferThreshold = 200,
                MaxRequestNewAddressRetry = 3,
                MaxRequestRetryTimes = 3,
                AssignedAddressExpiredHour = 48,
                TransferAddressLists = new Dictionary<string, List<string>>(),
                AddressWhiteLists = new List<string> { "0x1234567890abcdef1234567890abcdef12345678" },
                SupportCoins = new List<string> { "USDT", "ELF" },
                EVMCoins = new List<string> { "USDT", "ELF" }
            });
        return mockOptionsSnapshot.Object;
    }

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

    // mock ServiceFeeOptions
    /*
     * public bool IsOpen { get; set; } = true;
       public Dictionary<string, decimal> AmountThreshold { get; set; } = new();
       public Dictionary<string, decimal> MinThirdPartFee { get; set; } = new();
       public Dictionary<string, decimal> MaxThirdPartFee { get; set; } = new();
       public decimal FeeFluctuationPercent { get; set; } = (decimal)0.1;
       public int ThirdPartFeeExpireSeconds { get; set; } = 180;
       public Dictionary<string, decimal> MinAmount { get; set; } = new();
       public decimal MinWithdraw { get; set; } = 0.2M;
       public Dictionary<string, decimal> MinDeposit { get; set; } = new();
       public List<string> WithdrawFeeNetwork { get; set; }
       public int ThirdPartCacheFeeExpireSeconds { get; set; } = 180;
     */
    private IOptionsSnapshot<ServiceFeeOptions> MockServiceFeeOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<ServiceFeeOptions>>();
        mockOptionsSnapshot.Setup(o => o.Value).Returns(
            new ServiceFeeOptions
            {
                IsOpen = true,
                AmountThreshold = new Dictionary<string, decimal>
                {
                    { "USDT", 1000 },
                    { "ELF", 500 }
                },
                MinThirdPartFee = new Dictionary<string, decimal>
                {
                    { "USDT", 0.01m },
                    { "ELF", 0.001m }
                },
                MaxThirdPartFee = new Dictionary<string, decimal>
                {
                    { "USDT", 10m },
                    { "ELF", 1m }
                },
                FeeFluctuationPercent = 0.1m,
                ThirdPartFeeExpireSeconds = 180,
                MinAmount = new Dictionary<string, decimal>
                {
                    { "USDT", 10m },
                    { "ELF", 1m }
                },
                MinWithdraw = 0.2m,
                MinDeposit = new Dictionary<string, decimal>
                {
                    { "USDT", 5m },
                    { "ELF", 0.5m }
                },
                WithdrawFeeNetwork = new List<string> { "ETH", "AELF" },
                ThirdPartCacheFeeExpireSeconds = 180
            });
        return mockOptionsSnapshot.Object;
    }
}