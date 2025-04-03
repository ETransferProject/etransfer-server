using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Entities;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Token;
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

    private IOptionsSnapshot<TokenOptions> MockTokenOptions()
    {
        var mockOptionsSnapshot = new Mock<IOptionsSnapshot<TokenOptions>>();
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
                            Decimals = 6,
                            Icon = "icon1"
                        }
                    }
                },
                Deposit = new Dictionary<string, List<TokenConfig>>()
                {
                    ["AELF"] = new List<TokenConfig>()
                    {
                        new TokenConfig()
                        {
                            Symbol = "USDT",
                            Name = "USDT",
                            Decimals = 6,
                            Icon = "icon2"
                        }
                    }
                },
                DepositSwap = new List<TokenSwapConfig>()
                {
                    new TokenSwapConfig()
                    {
                        Symbol = "USDT",
                        Name = "USDT",
                        Decimals = 6,
                        ToTokenList = new List<ToTokenConfig>()
                        {
                            new ToTokenConfig()
                            {
                                Symbol = "ELF",
                                Name = "ELF",
                                ChainIdList = new List<string>() { "AELF" }
                            }
                        }
                    }
                }
            });
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
}