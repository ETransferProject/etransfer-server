using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.User;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User;

namespace ETransferServer.Grains.Grain.Timers;

public interface ICoBoDepositQueryTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallbackTime();

    Task CreateDepositOrder(CoBoTransactionDto depositOrder);
    Task<bool> GetDepositOrder(CoBoTransactionDto coBoTransaction);
}

public class CoBoDepositQueryTimerGrain : Grain<CoBoOrderState>, ICoBoDepositQueryTimerGrain
{
    private const int PageSize = 50;
    private DateTime? _lastCallbackTime;

    private readonly ILogger<CoBoDepositQueryTimerGrain> _logger;
    private readonly IOptionsMonitor<TimerOptions> _timerOptions;
    private readonly IOptionsMonitor<DepositOptions> _depositOption;
    private readonly IOptionsMonitor<NetworkOptions> _networkOption;
    private readonly IUserAppService _userAppService;
    private readonly IUserAddressService _userAddressService;

    private readonly ICoBoProvider _coBoProvider;

    public CoBoDepositQueryTimerGrain(ILogger<CoBoDepositQueryTimerGrain> logger,
        IOptionsMonitor<TimerOptions> timerOptions, ICoBoProvider coBoProvider,
        IOptionsMonitor<DepositOptions> depositOption, IOptionsMonitor<NetworkOptions> networkOption,
        IUserAppService userAppService, IUserAddressService userAddressService)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _coBoProvider = coBoProvider;
        _depositOption = depositOption;
        _networkOption = networkOption;
        _userAppService = userAppService;
        _userAddressService = userAddressService;
    }

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("CoBoDepositQueryTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync();

        if (State.ExistOrders == null)
        {
            State.ExistOrders = new List<string>();
        }

        await StartTimer(TimeSpan.FromSeconds(_timerOptions.CurrentValue.CoBoDepositQueryTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.CurrentValue.CoBoDepositQueryTimer.DelaySeconds));
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("CoBoDepositQueryTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(TimerCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task TimerCallback(object state)
    {
        _lastCallbackTime = DateTime.UtcNow;
        _logger.LogInformation("CoBoDepositQueryTimerGrain callback, {Time}", DateTime.UtcNow.ToUtc8String());

        var offset = 0;
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        var maxTime = State.LastTime;
        var transIds = new List<string>();

        while (true)
        {
            var list = new List<CoBoTransactionDto>();
            try
            {
                list = await _coBoProvider.GetTransactionsByTimeExAsync(new TransactionRequestDto
                {
                    Side = CoBoConstant.CoBoTransactionSideEnum.TransactionDeposit,
                    Status = CoBoConstant.CoBoTransactionStatusEnum.Success,
                    BeginTime = State.LastTime,
                    EndTime = now,
                    Order = CoBoConstant.Order.Asc,
                    Limit = PageSize,
                    Offset = offset,
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "get deposit transaction error.");
            }

            if (list.IsNullOrEmpty()) break;
            offset += list.Count;
            maxTime = list.Max(t => t.CreatedTime);
            foreach (var coBoTransaction in list)
            {
                transIds.Add(coBoTransaction.Id);
                if (State.ExistOrders.Contains(coBoTransaction.Id))
                {
                    _logger.LogInformation("order already handle: {orderId}",
                        coBoTransaction.Id);
                    continue;
                }

                _logger.LogInformation("create deposit order, orderInfo:{orderInfo}",
                    JsonConvert.SerializeObject(coBoTransaction));
                await CreateDepositOrder(coBoTransaction);
            }

            if (list.Count < PageSize) break;
        }


        State.ExistOrders = transIds;
        State.LastTime = maxTime;
        await WriteStateAsync();
    }

    public async Task CreateDepositOrder(CoBoTransactionDto coBoTransaction)
    {
        try
        {
            var orderDto = await ConvertToDepositOrderDto(coBoTransaction);

            // Query existing orders first to prevent repeated additions.
            var userDepositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderDto.Id);
            var orderExists = await userDepositRecordGrain.GetAsync();
            AssertHelper.IsTrue(orderExists.Success, "Query deposit exists order failed {Msg}", orderExists.Message);
            AssertHelper.IsNull(orderExists.Data, "Deposit order {OrderId} exists", orderDto.Id);

            // Save the order, UserDepositGrain will process the database multi-write and put the order into the stream for processing.
            var userDepositGrain = GrainFactory.GetGrain<IUserDepositGrain>(orderDto.Id);
            await userDepositGrain.AddOrUpdateOrder(orderDto);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Create deposit order error, coBoTransactionId={CoBoTxId}, requestId={CoBoRequestId}, coBoSymbol={Symbol}, amount={Amount}",
                coBoTransaction.TxId, coBoTransaction.RequestId, coBoTransaction.Coin, coBoTransaction.AbsAmount);
        }
    }
    public async Task<bool> GetDepositOrder(CoBoTransactionDto coBoTransaction)
    {
        try
        {
            var orderDto = await ConvertToDepositOrderDto(coBoTransaction);
            var userDepositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderDto.Id);
            var orderExists = await userDepositRecordGrain.GetAsync();
            if (orderExists is { Success: true })
            {
                return true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "GetDepositOrder get deposit order error, coBoTransactionId={CoBoTxId}, requestId={CoBoRequestId}, coBoSymbol={Symbol}, amount={Amount}",
                coBoTransaction.TxId, coBoTransaction.RequestId, coBoTransaction.Coin, coBoTransaction.AbsAmount);
        }
    
        return false;
    }

    private async Task<DepositOrderDto> ConvertToDepositOrderDto(CoBoTransactionDto coBoTransaction)
    {
        var coinInfo = CoBoHelper.MatchNetwork(coBoTransaction.Coin, _networkOption.CurrentValue.CoBo);

        var addressGrain = GrainFactory.GetGrain<IUserTokenDepositAddressGrain>(coBoTransaction.Address);
        var res = await addressGrain.Get();
        var userAddress = !res.Success || res.Data == null
            ? await _userAddressService.GetAssignedAddressAsync(coBoTransaction.Address)
            : res.Data as UserAddressDto;
        
        AssertHelper.NotNull(userAddress, "user address empty");
        AssertHelper.NotEmpty(userAddress.UserId, "address user id empty");

        var user = await _userAppService.GetUserByIdAsync(userAddress.UserId);
        AssertHelper.NotNull(user, "user empty");

        var addressInfo = user.AddressInfos.First(t => t.ChainId == userAddress.ChainId);
        AssertHelper.NotNull(addressInfo, "addressInfo empty");

        var paymentAddressExists =
            _depositOption.CurrentValue.PaymentAddresses?.ContainsKey(addressInfo.ChainId) ?? false;
        AssertHelper.IsTrue(paymentAddressExists, "Payment address missing, ChainId={ChainId}", addressInfo.ChainId);
        var paymentAddress = _depositOption.CurrentValue.PaymentAddresses.GetValueOrDefault(addressInfo.ChainId);
        AssertHelper.NotEmpty(paymentAddress, "Payment address empty, ChainId={ChainId}", addressInfo.ChainId);

        return new DepositOrderDto
        {
            Id = OrderIdHelper.DepositOrderId(coinInfo.Network, coinInfo.Symbol, coBoTransaction.TxId),
            OrderType = OrderTypeEnum.Deposit.ToString(),
            Status = OrderStatusEnum.FromTransferConfirmed.ToString(),

            ThirdPartOrderId = coBoTransaction.Id,
            ThirdPartServiceName = ThirdPartServiceNameEnum.Cobo.ToString(),
            UserId = user.UserId,
            FromTransfer = new TransferInfo
            {
                Network = coinInfo.Network,
                Symbol = coinInfo.Symbol,
                Status = OrderTransferStatusEnum.Confirmed.ToString(),
                TxId = coBoTransaction.TxId,
                Amount = Convert.ToDecimal(coBoTransaction.AbsAmount),
                FromAddress = coBoTransaction.SourceAddress,
                ToAddress = coBoTransaction.Address,
            },
            ToTransfer = new TransferInfo
            {
                FromAddress = paymentAddress,
                ToAddress = addressInfo.Address,
                Network = coinInfo.Network,
                ChainId = addressInfo.ChainId,
                Symbol = coinInfo.Symbol,
                Amount = coBoTransaction.AbsAmount.SafeToDecimal(),
                Status = OrderTransferStatusEnum.Created.ToString(),
            }
        };
    }


    public Task<DateTime> GetLastCallbackTime()
    {
        return Task.FromResult(_lastCallbackTime ?? DateTime.MinValue);
    }
}