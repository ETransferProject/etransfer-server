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
using NBitcoin;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface ICoBoDepositQueryTimerGrain : IGrainWithGuidKey
{
    public Task<DateTime> GetLastCallbackTime();
    Task SaveDepositRecord(CoBoTransactionDto depositOrder);
    Task CreateDepositRecord(CoBoTransactionDto depositOrder);
    Task Remove(string transactionId);
}

public class CoBoDepositQueryTimerGrain : Grain<CoBoOrderState>, ICoBoDepositQueryTimerGrain
{
    private const int PageSize = 50;
    private DateTime? _lastCallbackTime;

    private readonly ILogger<CoBoDepositQueryTimerGrain> _logger;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IOptionsSnapshot<DepositOptions> _depositOption;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOption;
    private readonly IOptionsSnapshot<NetworkOptions> _networkOption;
    private readonly IUserAppService _userAppService;
    private readonly IUserAddressService _userAddressService;

    private readonly ICoBoProvider _coBoProvider;
    private readonly IUserDepositProvider _userDepositProvider;
    private IDepositOrderStatusReminderGrain _depositOrderStatusReminderGrain;

    public CoBoDepositQueryTimerGrain(ILogger<CoBoDepositQueryTimerGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions, 
        ICoBoProvider coBoProvider,
        IUserDepositProvider userDepositProvider,
        IOptionsSnapshot<DepositOptions> depositOption, 
        IOptionsSnapshot<DepositAddressOptions> depositAddressOption, 
        IOptionsSnapshot<NetworkOptions> networkOption,
        IUserAppService userAppService, 
        IUserAddressService userAddressService)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _coBoProvider = coBoProvider;
        _userDepositProvider = userDepositProvider;
        _depositOption = depositOption;
        _depositAddressOption = depositAddressOption;
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

        await StartTimer(TimeSpan.FromSeconds(_timerOptions.Value.CoBoDepositQueryTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.Value.CoBoDepositQueryTimer.DelaySeconds));

        _depositOrderStatusReminderGrain =
            GrainFactory.GetGrain<IDepositOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderStatusReminderGrain)));
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
                if (State.ExistOrders.Contains(coBoTransaction.Id))
                {
                    _logger.LogInformation("order already handle: {orderId}",
                        coBoTransaction.Id);
                    continue;
                }

                await AddAfter(coBoTransaction);
                _logger.LogInformation("create deposit order, orderInfo:{orderInfo}",
                    JsonConvert.SerializeObject(coBoTransaction));
                await CreateDepositOrder(coBoTransaction);
            }

            if (list.Count < PageSize) break;
        }

        State.LastTime = maxTime;
        await WriteStateAsync();
    }

    private async Task CreateDepositOrder(CoBoTransactionDto coBoTransaction)
    {
        try
        {
            if (_depositAddressOption.Value.AddressWhiteLists.Contains(coBoTransaction.Address))
            {
                _logger.LogInformation("Deposit hit address whiteList: {address}", coBoTransaction.Address);
                await Remove(coBoTransaction.Id);
                return;
            }

            var orderDto = await ConvertToDepositOrderDto(coBoTransaction);

            // Query existing orders first to prevent repeated additions.
            var userDepositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderDto.Id);
            var orderExists = await userDepositRecordGrain.GetAsync();
            AssertHelper.IsTrue(orderExists.Success, "Query deposit exists order failed {Msg}", orderExists.Message);
            AssertHelper.IsTrue(orderExists.Value == null || orderExists.Value.Status == OrderStatusEnum.FromTransferring.ToString(), 
                "Deposit order {OrderId} exists", orderDto.Id);

            // Save the order, UserDepositGrain will process the database multi-write and put the order into the stream for processing.
            var userDepositGrain = GrainFactory.GetGrain<IUserDepositGrain>(orderDto.Id);
            await userDepositGrain.AddOrUpdateOrder(orderDto);
            await AddCheckDepositOrder(GuidHelper.GenerateId(coBoTransaction.Id, orderDto.Id.ToString()));
        }
        catch (Exception e)
        {
            var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(coBoTransaction.Id);
            if (await coBoDepositGrain.NotUpdated())
            {
                await AddCheckDepositOrder(GuidHelper.GenerateCombinedId(coBoTransaction.Id, GetAlarmTemplate(e)));
            }

            _logger.LogError(e,
                "Create deposit order error, coBoTransactionId={CoBoTxId}, requestId={CoBoRequestId}, coBoSymbol={Symbol}, amount={Amount}",
                coBoTransaction.TxId, coBoTransaction.RequestId, coBoTransaction.Coin, coBoTransaction.AbsAmount);
        }
    }
    
    private string GetAlarmTemplate(Exception e)
    {
        if (e is UserFriendlyException userFriendlyException)
        {
            if (userFriendlyException.Code == ErrorResult.CoBoCoinInvalid.ToString() ||
                userFriendlyException.Code == ErrorResult.CoBoCoinNotSupport.ToString())
            {
                return CommonConstant.DepositOrderCoinNotSupportAlarm;
            }
        }

        return CommonConstant.DepositOrderLostAlarm;
    }
    

    public async Task Remove(string transactionId)
    {
        State.ExistOrders.Remove(transactionId);
        await WriteStateAsync();
    }

    private async Task<DepositOrderDto> ConvertToDepositOrderDto(CoBoTransactionDto coBoTransaction)
    {
        var coinInfo = CoBoHelper.MatchNetwork(coBoTransaction.Coin, _networkOption.Value.CoBo);

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
            _depositOption.Value.PaymentAddresses?.ContainsKey(addressInfo.ChainId) ?? false;
        AssertHelper.IsTrue(paymentAddressExists, "Payment address missing, ChainId={ChainId}", addressInfo.ChainId);
        var paymentAddressDic = _depositOption.Value.PaymentAddresses.GetValueOrDefault(addressInfo.ChainId);
        AssertHelper.NotEmpty(paymentAddressDic, "Payment address empty, ChainId={ChainId}", addressInfo.ChainId);
        
        var depositOrderDto = new DepositOrderDto
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
                BlockHash = coBoTransaction.TxDetail.BlockHash
            },
            ToTransfer = new TransferInfo
            {
                FromAddress = GetPaymentAddress(paymentAddressDic, coinInfo.Symbol),
                ToAddress = addressInfo.Address,
                Network = CommonConstant.Network.AElf,
                ChainId = addressInfo.ChainId,
                Symbol = coinInfo.Symbol,
                Amount = coBoTransaction.AbsAmount.SafeToDecimal(),
                Status = OrderTransferStatusEnum.Created.ToString(),
            }
        };

        return SpecialHandle(depositOrderDto, coBoTransaction, userAddress.ToSymbol);
    }

    private DepositOrderDto SpecialHandle(DepositOrderDto dto, CoBoTransactionDto coBoTransaction, string symbol)
    {
        _logger.LogInformation("SpecialHandle, input dto: {dto}", JsonConvert.SerializeObject(dto));
        // Add ExtensionInfo
        dto.ExtensionInfo ??= new Dictionary<string, string>();
        dto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum, coBoTransaction.ConfirmedNum.ToString());
        dto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold, coBoTransaction.TxDetail.ConfirmingThreshold.ToString());
        if (!coBoTransaction.Memo.IsNullOrWhiteSpace())
        {
            dto.ExtensionInfo.Add(ExtensionKey.Memo, coBoTransaction.Memo);
        }
        
        if (symbol.IsNullOrEmpty() || _depositOption.Value.NoSwapSymbols.Contains(dto.FromTransfer.Symbol))
        {
            _logger.LogInformation("SpecialHandle, not need swap, set ToTransfer.Symbol = FromTransfer.Symbol");
            return dto;
        }

        if (DepositSwapHelper.IsDepositSwap(dto.FromTransfer.Symbol, symbol))
        {
            _logger.LogInformation("SpecialHandle, need swap, set ExtensionInfo");
            dto.ExtensionInfo.Add(ExtensionKey.IsSwap, Boolean.TrueString);
            dto.ExtensionInfo.Add(ExtensionKey.NeedSwap, Boolean.TrueString);
            dto.ExtensionInfo.Add(ExtensionKey.SwapStage, SwapStage.SwapTx);
            dto.ToTransfer.Symbol = symbol;
            return dto;
        }
        
        _logger.LogInformation("SpecialHandle, default set ToTransfer.Symbol = FromTransfer.Symbol");
        return dto;
    }

    private string GetPaymentAddress(Dictionary<string, string> paymentAddressDic, string symbol)
    {
        var paymentAddress = paymentAddressDic.GetValueOrDefault(symbol);
        AssertHelper.NotEmpty(paymentAddress, "Payment address empty, Symbol={Symbol}", symbol);
        return paymentAddress;
    }

    private async Task AddAfter(CoBoTransactionDto depositOrder)
    {
        if (State.ExistOrders.Count > _depositOption.Value.MaxListLength)
            State.ExistOrders.RemoveAt(0);
        State.ExistOrders.Add(depositOrder.Id);

        var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(depositOrder.Id);
        await coBoDepositGrain.AddOrUpdate(depositOrder);
    }

    public async Task AddCheckDepositOrder(string id)
    {
        await _depositOrderStatusReminderGrain.AddReminder(id);
    }

    public Task<DateTime> GetLastCallbackTime()
    {
        return Task.FromResult(_lastCallbackTime ?? DateTime.MinValue);
    }

    public async Task SaveDepositRecord(CoBoTransactionDto coBoTransaction)
    {
        _logger.LogInformation("save deposit record, recordInfo:{recordInfo}",
            JsonConvert.SerializeObject(coBoTransaction));
        var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(coBoTransaction.Id);
        await coBoDepositGrain.AddOrUpdate(coBoTransaction);
        
        if (_depositAddressOption.Value.AddressWhiteLists.Contains(coBoTransaction.Address))
        {
            _logger.LogInformation("save deposit hit address whiteList: {address}", coBoTransaction.Address);
            return;
        }

        var orderDto = await ConvertToDepositOrderDto(coBoTransaction);
        orderDto.Status = OrderStatusEnum.FromTransferring.ToString();
        orderDto.FromTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
        orderDto.ToTransfer.Status = string.Empty;
        var userDepositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderDto.Id);
        var res = await userDepositRecordGrain.CreateOrUpdateAsync(orderDto);
        await _userDepositProvider.AddOrUpdateSync(res.Value);
    }

    public async Task CreateDepositRecord(CoBoTransactionDto coBoTransaction)
    {
        if (State.ExistOrders.Contains(coBoTransaction.Id))
        {
            _logger.LogInformation("order already handle: {orderId}",
                coBoTransaction.Id);
            return;
        }

        await AddAfter(coBoTransaction);
        _logger.LogInformation("create deposit record, recordInfo:{recordInfo}",
            JsonConvert.SerializeObject(coBoTransaction));
        
        await CreateDepositOrder(coBoTransaction);
        await WriteStateAsync();
    }
}