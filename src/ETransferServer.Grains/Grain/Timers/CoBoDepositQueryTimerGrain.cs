using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Token;
using ETransferServer.Dtos.User;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User;
using MassTransit;
using NBitcoin;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

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
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    private readonly IUserAppService _userAppService;
    private readonly IUserAddressService _userAddressService;
    private readonly INetworkAppService _networkService;

    private readonly ICoBoProvider _coBoProvider;
    private readonly IUserDepositProvider _userDepositProvider;
    private IDepositOrderStatusReminderGrain _depositOrderStatusReminderGrain;
    private readonly IObjectMapper _objectMapper;
    private readonly IBus _bus;

    public CoBoDepositQueryTimerGrain(ILogger<CoBoDepositQueryTimerGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions, 
        ICoBoProvider coBoProvider,
        IUserDepositProvider userDepositProvider,
        IOptionsSnapshot<DepositOptions> depositOption, 
        IOptionsSnapshot<DepositAddressOptions> depositAddressOption, 
        IOptionsSnapshot<NetworkOptions> networkOption,
        IOptionsSnapshot<CoBoOptions> coBoOptions,
        IUserAppService userAppService, 
        IUserAddressService userAddressService,
        INetworkAppService networkService,
        IObjectMapper objectMapper, 
        IBus bus)
    {
        _logger = logger;
        _timerOptions = timerOptions;
        _coBoProvider = coBoProvider;
        _userDepositProvider = userDepositProvider;
        _depositOption = depositOption;
        _depositAddressOption = depositAddressOption;
        _networkOption = networkOption;
        _coBoOptions = coBoOptions;
        _userAppService = userAppService;
        _userAddressService = userAddressService;
        _networkService = networkService;
        _objectMapper = objectMapper;
        _bus = bus;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("CoBoDepositQueryTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync(cancellationToken);

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
        _logger.LogInformation("CoBoDepositQueryTimerGrain lastTime: {LastTime},{GrainId},{Key},{Count}", 
            maxTime, this.GetGrainId(), this.GetPrimaryKey(), State.ExistOrders.Count);

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
                if (coBoTransaction.AbsAmount.SafeToDecimal() <= 0M)
                {
                    _logger.LogInformation("transaction amount invalid");
                    continue;
                }
                if (State.ExistOrders.Contains(coBoTransaction.Id))
                {
                    _logger.LogInformation("order already handle: {orderId}",
                        coBoTransaction.Id);
                    continue;
                }

                // await AddAfter(coBoTransaction);
                _logger.LogInformation("create deposit order, orderInfo:{orderInfo}",
                    JsonConvert.SerializeObject(coBoTransaction));
                // await CreateDepositOrder(coBoTransaction);
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
            if (orderDto.ToTransfer.Amount == 0)
            {
                orderDto.Status = OrderStatusEnum.Finish.ToString();
                orderDto.ToTransfer.Status = OrderTransferStatusEnum.Confirmed.ToString();
                var res = await userDepositRecordGrain.CreateOrUpdateAsync(orderDto);
                await _userDepositProvider.AddOrUpdateSync(res.Value);
                await _bus.Publish(_objectMapper.Map<DepositOrderDto, OrderChangeEto>(res.Value));
                return;
            }
            if (orderExists.Value == null)
            {
                await _bus.Publish(_objectMapper.Map<DepositOrderDto, OrderChangeEto>(orderDto));
            }
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
                await Remove(coBoTransaction.Id);
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

        var addressInfo = user.AddressInfos.FirstOrDefault(t => t.ChainId == userAddress.ChainId);
        if (addressInfo == null) addressInfo = user.AddressInfos.FirstOrDefault();
        AssertHelper.NotNull(addressInfo, "addressInfo empty");

        var paymentAddressExists =
            _depositOption.Value.PaymentAddresses?.ContainsKey(userAddress.ChainId) ?? false;
        AssertHelper.IsTrue(paymentAddressExists, "Payment address missing, ChainId={ChainId}", userAddress.ChainId);
        var paymentAddressDic = _depositOption.Value.PaymentAddresses.GetValueOrDefault(userAddress.ChainId);
        AssertHelper.NotEmpty(paymentAddressDic, "Payment address empty, ChainId={ChainId}", userAddress.ChainId);
        var (isOpen, serviceFee, minAmount) = await GetServiceFeeAsync(coinInfo.Network, coinInfo.Symbol);
        var toAmount = isOpen && coBoTransaction.AbsAmount.SafeToDecimal() >= minAmount
            ? coBoTransaction.AbsAmount.SafeToDecimal() - serviceFee
            : !isOpen && coBoTransaction.AbsAmount.SafeToDecimal() >= minAmount
                ? coBoTransaction.AbsAmount.SafeToDecimal()
                : 0M;
        toAmount = toAmount < 0 ? 0M : toAmount;

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
                FromAddress = KeyMapping(coBoTransaction.SourceAddress),
                ToAddress = coBoTransaction.Address,
                BlockHash = coBoTransaction.TxDetail.BlockHash
            },
            ToTransfer = new TransferInfo
            {
                FromAddress = GetPaymentAddress(paymentAddressDic, coinInfo.Symbol),
                ToAddress = addressInfo.Address,
                Network = CommonConstant.Network.AElf,
                ChainId = userAddress.ChainId,
                Symbol = coinInfo.Symbol,
                Amount = toAmount,
                Status = OrderTransferStatusEnum.Created.ToString()
            }
        };
        if (isOpen)
        {
            depositOrderDto.ToTransfer.FeeInfo = new List<FeeInfo>
            {
                new(coinInfo.Symbol, coBoTransaction.AbsAmount.SafeToDecimal() >= serviceFee
                ? serviceFee.ToString() : coBoTransaction.AbsAmount.SafeToDecimal().ToString(
                    DecimalHelper.GetDecimals(coinInfo.Symbol), DecimalHelper.RoundingOption.Floor))
            };
        }

        return SpecialHandle(depositOrderDto, coBoTransaction, userAddress.ToSymbol, toAmount);
    }

    private DepositOrderDto SpecialHandle(DepositOrderDto dto, CoBoTransactionDto coBoTransaction, string symbol, decimal amount)
    {
        _logger.LogInformation("SpecialHandle, input dto: {dto}", JsonConvert.SerializeObject(dto));
        // Add ExtensionInfo
        dto.ExtensionInfo ??= new Dictionary<string, string>();
        dto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum, coBoTransaction.ConfirmedNum.ToString());
        dto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold, coBoTransaction.ConfirmingThreshold > 0
            ? coBoTransaction.ConfirmingThreshold.ToString()
            : coBoTransaction.TxDetail.ConfirmingThreshold.ToString());
        if (!coBoTransaction.Memo.IsNullOrWhiteSpace())
        {
            dto.ExtensionInfo.AddOrReplace(ExtensionKey.Memo, coBoTransaction.Memo);
        }
        
        if (amount == 0 || symbol.IsNullOrEmpty() || _depositOption.Value.NoSwapSymbols.Contains(dto.FromTransfer.Symbol))
        {
            _logger.LogInformation("SpecialHandle, not need swap, set ToTransfer.Symbol = FromTransfer.Symbol");
            return dto;
        }

        if (DepositSwapHelper.IsDepositSwap(dto.FromTransfer.Symbol, symbol))
        {
            _logger.LogInformation("SpecialHandle, need swap, set ExtensionInfo");
            dto.ExtensionInfo.AddOrReplace(ExtensionKey.IsSwap, Boolean.TrueString);
            dto.ExtensionInfo.AddOrReplace(ExtensionKey.NeedSwap, Boolean.TrueString);
            dto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapStage, SwapStage.SwapTx);
            dto.ToTransfer.Symbol = symbol;
            if (dto.ToTransfer.ChainId == ChainId.AELF)
            {
                _logger.LogInformation("Swap to mainChain, {fromSymbol}, {toSymbol}", dto.FromTransfer.Symbol, symbol);
                dto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapToMain, Boolean.TrueString);
                dto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapFromAddress, GetPaymentAddress(
                    _depositOption.Value.PaymentAddresses.GetValueOrDefault(dto.ToTransfer.ChainId), symbol));
                dto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapToAddress, dto.ToTransfer.ToAddress);
                dto.ExtensionInfo.AddOrReplace(ExtensionKey.SwapChainId, dto.ToTransfer.ChainId);
                var sideChainId = _depositOption.Value.PaymentAddresses.Keys.FirstOrDefault(t => t != ChainId.AELF);
                var paymentAddressDic = _depositOption.Value.PaymentAddresses.GetValueOrDefault(sideChainId);
                dto.ToTransfer.FromAddress = GetPaymentAddress(paymentAddressDic, dto.FromTransfer.Symbol);
                dto.ToTransfer.ToAddress = GetPaymentAddress(paymentAddressDic, symbol);
                dto.ToTransfer.ChainId = sideChainId;
            }

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
    
    private async Task<Tuple<bool, decimal, decimal>> GetServiceFeeAsync(string network, string symbol)
    {
        var isOpen = _depositOption.Value.ServiceFee.IsOpen;
        var (estimateFee, coin) = network == ChainId.AELF || network == ChainId.tDVV || network == ChainId.tDVW
            ? Tuple.Create(0M, new CoBoCoinDto { ExpireTime = 0L })
            : await _networkService.CalculateNetworkFeeAsync(network, symbol);
        var feeKey = string.Join(CommonConstant.Underline, network, symbol);
        var serviceFee = Math.Min(estimateFee, _depositOption.Value.ServiceFee.MaxThirdPartFee.ContainsKey(feeKey)
            ? _depositOption.Value.ServiceFee.MaxThirdPartFee[feeKey]
            : 0M).ToString(2, DecimalHelper.RoundingOption.Ceiling).SafeToDecimal();
        var minAmount = _depositOption.Value.ServiceFee.MinAmount.ContainsKey(feeKey)
            ? _depositOption.Value.ServiceFee.MinAmount[feeKey]
            : 0M;
        _logger.LogDebug("Grain Deposit from network fee: {network}, {symbol}, {isOpen}, {serviceFee}, {minAmount}", 
            network, symbol, isOpen, serviceFee, minAmount);
        return Tuple.Create(isOpen, serviceFee, minAmount);
    }

    private async Task AddAfter(CoBoTransactionDto depositOrder)
    {
        if (State.ExistOrders.Count > _depositOption.Value.MaxListLength)
            State.ExistOrders.RemoveAt(0);
        State.ExistOrders.Add(depositOrder.Id);

        var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(depositOrder.Id);
        await coBoDepositGrain.AddOrUpdate(depositOrder);
    }
    
    private string KeyMapping(string key)
    {
        return _coBoOptions.Value.KeyMapping.GetValueOrDefault(key, key);
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
        var coBoDto = await coBoDepositGrain.Get();
        if (coBoDto != null && coBoDto.Status == CommonConstant.SuccessStatus)
        {
            _logger.LogInformation("order already success: {id}", coBoTransaction.Id);
            return;
        }
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
        var recordDto = await userDepositRecordGrain.GetAsync();
        if (recordDto.Value == null)
        {
            await _bus.Publish(_objectMapper.Map<DepositOrderDto, OrderChangeEto>(orderDto));
        }
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