using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo.Dtos;
using MassTransit;
using NBitcoin;
using Newtonsoft.Json;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IUserWithdrawGrain : IGrainWithGuidKey
{
    /// <summary>
    ///     To create a coin order,
    ///     the From Transaction will be filled with the target address.
    /// </summary>
    /// <param name="withdrawOrderDto"></param>
    /// <returns></returns>
    Task<WithdrawOrderDto> CreateOrder(WithdrawOrderDto withdrawOrderDto);
    
    Task CreateTransferOrder(WithdrawOrderDto withdrawOrderDto);

    Task SaveTransferOrder(CoBoTransactionDto coBoTransaction);
    
    Task<WithdrawOrderDto> CreateRefundOrder(WithdrawOrderDto withdrawOrderDto, string address);

    /// <summary>
    ///     Forward transaction information from the front end
    ///     The order number, transfer target, transfer amount and transfer symbol corresponding to the transaction will be verified.
    ///     After the verification is passed, the transaction will be bound to the order and sent to stream for processing.
    ///     The stream will process the transaction submission to the node, multiple confirmations,
    ///     and finally call the external interface to raise money to the address specified by the user.
    /// </summary>
    /// <param name="withdrawOrderDto"></param>
    /// <returns></returns>
    Task<WithdrawOrderDto> TransferForward(WithdrawOrderDto withdrawOrderDto);

    /// <summary>
    ///     Please use <see cref="CreateOrder"/> for new order
    ///     This method mainly used by Timer to return the order to Stream
    /// </summary>
    /// <param name="orderDto"></param>
    /// <param name="externalInfo"></param>
    /// <returns></returns>
    Task<WithdrawOrderDto> AddOrUpdateOrder(WithdrawOrderDto orderDto, Dictionary<string, string> externalInfo = null);
}

public partial class UserWithdrawGrain : Orleans.Grain, IAsyncObserver<WithdrawOrderDto>, IUserWithdrawGrain
{
    private readonly ILogger<UserWithdrawGrain> _logger;
    private IAsyncStream<WithdrawOrderDto> _orderChangeStream;

    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<DepositAddressOptions> _depositAddressOption;

    private IUserWithdrawRecordGrain _recordGrain;
    private IOrderStatusFlowGrain _orderStatusFlowGrain;
    private IOrderTxFlowGrain _orderTxFlowGrain;
    private IUserWithdrawTxTimerGrain _withdrawTxTimerGrain;
    private IUserWithdrawTxFastTimerGrain _withdrawFastTimerGrain;
    private IWithdrawOrderRetryTimerGrain _withdrawOrderRetryTimerGrain;
    private IWithdrawCoboTimerGrain _withdrawCoboTimerGrain;
    private IWithdrawTimerGrain _withdrawTimerGrain;
    private IOrderStatusReminderGrain _orderStatusReminderGrain;
    private IWithdrawQueryTimerGrain _withdrawQueryTimerGrain;

    private readonly IContractProvider _contractProvider;
    private readonly IUserWithdrawProvider _userWithdrawProvider;
    private readonly IOrderStatusFlowProvider _orderStatusFlowProvider;
    private readonly IUserAddressProvider _userAddressProvider;
    private readonly IObjectMapper _objectMapper;
    private readonly IBus _bus;

    internal JsonSerializerSettings JsonSettings = JsonSettingsBuilder.New()
        .WithAElfTypesConverters()
        .WithCamelCasePropertyNamesResolver()
        .Build();

    private const int MaxStreamSteps = 100;
    private int _currentSteps = 0;

    public UserWithdrawGrain(IUserWithdrawProvider userWithdrawProvider,
        ILogger<UserWithdrawGrain> logger, IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOptions, IContractProvider contractProvider,
        IOrderStatusFlowProvider orderStatusFlowProvider,
        IUserAddressProvider userAddressProvider,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<DepositAddressOptions> depositAddressOption,
        IObjectMapper objectMapper, 
        IBus bus)
    {
        _userWithdrawProvider = userWithdrawProvider;
        _logger = logger;
        _chainOptions = chainOptions;
        _withdrawOptions = withdrawOptions;
        _contractProvider = contractProvider;
        _orderStatusFlowProvider = orderStatusFlowProvider;
        _userAddressProvider = userAddressProvider;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _depositAddressOption = depositAddressOption;
        _objectMapper = objectMapper;
        _bus = bus;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StreamProvider withdraw subscribe start, {Key}", this.GetPrimaryKey());
        await base.OnActivateAsync(cancellationToken);

        // subscribe stream
        var streamProvider = this.GetStreamProvider(CommonConstant.StreamConstant.MessageStreamNameSpace);
        _orderChangeStream =
            streamProvider.GetStream<WithdrawOrderDto>(_withdrawOptions.Value.OrderChangeTopic,
                this.GetPrimaryKey());
        await _orderChangeStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);
        _logger.LogInformation("StreamProvider withdraw subscribe ok.");

        // other grain
        _recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(this.GetPrimaryKey());
        _orderStatusFlowGrain = GrainFactory.GetGrain<IOrderStatusFlowGrain>(this.GetPrimaryKey());
        _orderTxFlowGrain = GrainFactory.GetGrain<IOrderTxFlowGrain>(this.GetPrimaryKey());
        
        _withdrawTxTimerGrain =
            GrainFactory.GetGrain<IUserWithdrawTxTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserWithdrawTxTimerGrain)));
        _withdrawFastTimerGrain =
            GrainFactory.GetGrain<IUserWithdrawTxFastTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserWithdrawTxFastTimerGrain)));
        _withdrawOrderRetryTimerGrain =
            GrainFactory.GetGrain<IWithdrawOrderRetryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawOrderRetryTimerGrain)));
        _withdrawCoboTimerGrain =
            GrainFactory.GetGrain<IWithdrawCoboTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawCoboTimerGrain)));
        _withdrawTimerGrain =
            GrainFactory.GetGrain<IWithdrawTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
        _orderStatusReminderGrain = 
            GrainFactory.GetGrain<IOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IOrderStatusReminderGrain)));
        _withdrawQueryTimerGrain =
            GrainFactory.GetGrain<IWithdrawQueryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawQueryTimerGrain)));
    }

    public async Task<WithdrawOrderDto> CreateOrder(WithdrawOrderDto withdrawOrderDto)
    {
        AssertHelper.NotNull(withdrawOrderDto, ErrorResult.TransactionFailCode);
        AssertHelper.IsTrue(withdrawOrderDto.OrderType == OrderTypeEnum.Withdraw.ToString(),
            ErrorResult.TransactionFailCode);
        AssertHelper.NotNull(withdrawOrderDto.FromTransfer, ErrorResult.TransactionFailCode);
        AssertHelper.NotEmpty(withdrawOrderDto.FromTransfer.ChainId, ErrorResult.TransactionFailCode);
        AssertHelper.NotEmpty(withdrawOrderDto.RawTransaction, ErrorResult.TransactionFailCode);
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer, ErrorResult.TransactionFailCode);
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.Network, ErrorResult.TransactionFailCode);
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.ToAddress, ErrorResult.TransactionFailCode);
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.Symbol, ErrorResult.TransactionFailCode);
        AssertHelper.IsTrue(withdrawOrderDto.ToTransfer.Amount > 0, ErrorResult.TransactionFailCode);
        
        var coinInfo = _withdrawNetworkOptions.Value.GetNetworkInfo(withdrawOrderDto.ToTransfer.Network,
            withdrawOrderDto.ToTransfer.Symbol);
        AssertHelper.IsTrue(coinInfo.Decimal >= 0, ErrorResult.TransactionFailCode);
        
        withdrawOrderDto.Id = this.GetPrimaryKey();
        withdrawOrderDto.Status = OrderStatusEnum.Created.ToString();
        withdrawOrderDto.FromTransfer.Network = CommonConstant.Network.AElf;
        withdrawOrderDto.ExtensionInfo ??= new Dictionary<string, string>();
        withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold, GetFromConfirmingThreshold(withdrawOrderDto).ToString());
        return await AddOrUpdateOrder(withdrawOrderDto);
    }

    public async Task CreateTransferOrder(WithdrawOrderDto withdrawOrderDto)
    {
        withdrawOrderDto.Id = this.GetPrimaryKey();
        withdrawOrderDto.FromTransfer.Status = OrderTransferStatusEnum.Created.ToString();
        withdrawOrderDto.Status = OrderStatusEnum.Created.ToString();
        var coBoCoinGrain =
            GrainFactory.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(withdrawOrderDto.FromTransfer.Network,
                withdrawOrderDto.FromTransfer.Symbol));
        withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold, (await coBoCoinGrain.GetConfirmingThreshold()).ToString());
        var res = await _recordGrain.AddOrUpdate(withdrawOrderDto);
        await _userWithdrawProvider.AddOrUpdateSync(res.Value);
    }
    
    public async Task SaveTransferOrder(CoBoTransactionDto coBoTransaction)
    {
        _logger.LogInformation("save transfer record, recordInfo:{recordInfo}",
            JsonConvert.SerializeObject(coBoTransaction));
        
        var orderDto = (await _recordGrain.Get())?.Value;
        if (orderDto == null)
        {
            _logger.LogInformation("transfer order empty: {id}", this.GetPrimaryKey());
            return;
        }

        if (!orderDto.ExtensionInfo.IsNullOrEmpty() &&
            orderDto.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus) &&
            orderDto.ExtensionInfo[ExtensionKey.SubStatus] == OrderOperationStatusEnum.UserTransferRejected.ToString())
        {
            _logger.LogInformation("transfer order already rejected: {id}", this.GetPrimaryKey());
            await TransferCallbackAlarmAsync(orderDto, coBoTransaction.Id, 
                "Received callback, the order has already been rejected.");
            return;
        }
        
        if (orderDto.FromTransfer.FromAddress.ToLower() != coBoTransaction.SourceAddress.ToLower()
            || orderDto.FromTransfer.ToAddress.ToLower() != coBoTransaction.Address.ToLower()
            || orderDto.FromTransfer.Amount != coBoTransaction.AbsAmount.SafeToDecimal()
            || GuidHelper.GenerateId(orderDto.FromTransfer.Network, orderDto.FromTransfer.Symbol) !=
            coBoTransaction.Coin
            || (!orderDto.ThirdPartOrderId.IsNullOrEmpty() && orderDto.ThirdPartOrderId != coBoTransaction.Id))
        {
            _logger.LogInformation("transfer order unable to match: {id}", this.GetPrimaryKey());
            return;
        }

        var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(coBoTransaction.Id);
        var coBoDto = await coBoDepositGrain.Get();
        if (coBoDto != null && coBoDto.Status == CommonConstant.SuccessStatus)
        {
            _logger.LogInformation("transfer order already success: {id}", this.GetPrimaryKey());
            return;
        }
        await coBoDepositGrain.AddOrUpdate(coBoTransaction);

        orderDto.ThirdPartOrderId = coBoTransaction.Id;
        orderDto.ThirdPartServiceName = ThirdPartServiceNameEnum.Cobo.ToString();
        orderDto.FromTransfer.TxId = coBoTransaction.TxId;
        orderDto.FromTransfer.TxTime ??= DateTimeHelper.ToUnixTimeMilliseconds(DateTime.UtcNow);
        orderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum, coBoTransaction.ConfirmedNum.ToString());
        orderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold, coBoTransaction.ConfirmingThreshold > 0
            ? coBoTransaction.ConfirmingThreshold.ToString()
            : coBoTransaction.TxDetail.ConfirmingThreshold.ToString());

        if (coBoTransaction.Status == CommonConstant.PendingStatus)
        {
            orderDto.FromTransfer.Status = OrderTransferStatusEnum.Transferring.ToString();
            orderDto.Status = OrderStatusEnum.FromTransferring.ToString();
            var res = await _recordGrain.AddOrUpdate(orderDto);
            await _userWithdrawProvider.AddOrUpdateSync(res.Value);
        }

        if (coBoTransaction.Status == CommonConstant.SuccessStatus)
        {
            orderDto.FromTransfer.Status = OrderTransferStatusEnum.Confirmed.ToString();
            orderDto.Status = OrderStatusEnum.FromTransferConfirmed.ToString();
            await AddOrUpdateOrder(orderDto);
        }
        if (coBoDto == null || coBoDto.Id == null)
        {
            _logger.LogInformation("transfer order send bus: {id}", this.GetPrimaryKey());
            await _bus.Publish(_objectMapper.Map<WithdrawOrderDto, OrderChangeEto>(orderDto));
        }
    }
    
    public async Task<WithdrawOrderDto> CreateRefundOrder(WithdrawOrderDto withdrawDto, string address)
    {
        // amount limit
        var amountUsd = await _withdrawQueryTimerGrain.GetAvgExchangeAsync(withdrawDto.FromTransfer.Symbol, CommonConstant.Symbol.USD);
        var tokenInfoGrain =
            GrainFactory.GetGrain<ITokenWithdrawLimitGrain>(ITokenWithdrawLimitGrain.GenerateGrainId(withdrawDto.FromTransfer.Symbol));
        AssertHelper.IsTrue(await tokenInfoGrain.Acquire(amountUsd), ErrorResult.WithdrawLimitInsufficientCode, null,
            (await tokenInfoGrain.GetLimit()).RemainingLimit, TimeHelper.GetHourDiff(DateTime.UtcNow,
                DateTime.UtcNow.AddDays(1).Date));
        try
        {
            var withdrawOrderDto = new WithdrawOrderDto
            {
                Id = this.GetPrimaryKey(),
                Status = OrderStatusEnum.FromTransferConfirmed.ToString(),
                UserId = await _withdrawQueryTimerGrain.GetUserIdAsync(address),
                OrderType = OrderTypeEnum.Withdraw.ToString(),
                AmountUsd = amountUsd,
                FromTransfer = withdrawDto.FromTransfer,
                ToTransfer = new TransferInfo
                {
                    Network = withdrawDto.FromTransfer.Network,
                    ChainId = withdrawDto.FromTransfer.ChainId,
                    FromAddress = withdrawDto.FromTransfer.ToAddress,
                    ToAddress = withdrawDto.FromTransfer.FromAddress,
                    Amount = withdrawDto.FromTransfer.Amount,
                    Symbol = withdrawDto.FromTransfer.Symbol
                }
            };

            withdrawOrderDto.ExtensionInfo ??= new Dictionary<string, string>();
            withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.RefundTx, ExtensionKey.RefundTx);
            withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.RelatedOrderId, withdrawDto.Id.ToString());
            if (!withdrawDto.ExtensionInfo.IsNullOrEmpty() &&
                withdrawDto.ExtensionInfo.ContainsKey(ExtensionKey.FromConfirmingThreshold))
            {
                withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmedNum,
                    withdrawDto.ExtensionInfo[ExtensionKey.FromConfirmingThreshold]);
                withdrawOrderDto.ExtensionInfo.AddOrReplace(ExtensionKey.FromConfirmingThreshold,
                    withdrawDto.ExtensionInfo[ExtensionKey.FromConfirmingThreshold]);
            }

            return await AddOrUpdateOrder(withdrawOrderDto);
        }
        catch (Exception e)
        {
            await tokenInfoGrain.Reverse(amountUsd);
            _logger.LogError(e, "Create refund withdraw order error, relatedOrderId:{OrderId}", withdrawDto.Id);
            throw;
        }
    }

    private long GetFromConfirmingThreshold(WithdrawOrderDto withdrawOrderDto)
    {
        var isAElf = withdrawOrderDto.ToTransfer.Network == CommonConstant.Network.AElf;
        if (isAElf)
        {
            _withdrawOptions.Value.Homogeneous.TryGetValue(withdrawOrderDto.FromTransfer.Symbol, out var threshold);
            var amountThreshold = threshold?.AmountThreshold ?? 0L;
            var blockHeightUpperThreshold = threshold?.BlockHeightUpperThreshold ?? 0L;
            var blockHeightLowerThreshold = threshold?.BlockHeightLowerThreshold ?? 0L;
            return withdrawOrderDto.FromTransfer.Amount <= amountThreshold
                ? blockHeightLowerThreshold
                : blockHeightUpperThreshold;
        }
        return _chainOptions.Value.Contract.SafeBlockHeight;
    }

    public async Task<WithdrawOrderDto> AddOrUpdateOrder(WithdrawOrderDto orderDto,
        Dictionary<string, string> externalInfo = null)
    {
        // save withdraw order to Grain
        var res = await _recordGrain.AddOrUpdate(orderDto);
        if (!res.Success)
        {
            _logger.LogError("save order data error, orderId = {Id}", orderDto.Id);
        }
        AssertHelper.IsTrue(res.Success, ErrorResult.OrderSaveFailCode);

        // save order status flow
        var orderFlowRes = await _orderStatusFlowGrain.AddAsync(orderDto.Status, externalInfo);

        // save withdraw order to ES
        await _userWithdrawProvider.AddOrUpdateSync(res.Value);

        // save order flow to ES
        await _orderStatusFlowProvider.AddOrUpdate(orderDto.Id, orderFlowRes);

        if (IsForward(orderDto, externalInfo)) return res.Value;
        // push order to stream
        _logger.LogInformation("push to stream, type:withdraw, orderId:{OrderId}, status:{Status}",
            orderDto.Id, orderDto.Status);
        await _orderChangeStream.OnNextAsync(orderDto);

        return res.Value;
    }

    private bool IsForward(WithdrawOrderDto orderDto, Dictionary<string, string> externalInfo)
    {
        if ((orderDto.Status == OrderStatusEnum.FromTransferConfirmed.ToString() ||
             orderDto.ToTransfer.Status == CommonConstant.PendingStatus) && !externalInfo.IsNullOrEmpty() &&
            externalInfo.ContainsKey(ExtensionKey.IsForward))
        {
            var result = bool.TryParse(externalInfo[ExtensionKey.IsForward], out var isForward);
            _logger.LogInformation("IsForward, type:withdraw, orderId:{OrderId}, isForward:{isForward}",
                orderDto.Id, isForward);
            return result && !isForward;
        }

        return false;
    }
    
    private async Task TransferCallbackAlarmAsync(WithdrawOrderDto orderDto, string id, string reason)
    {
        var dto = new OrderIndexDto
            {
                Id = orderDto.Id,
                FromTransfer = new TransferInfoDto
                {
                    Network = orderDto.FromTransfer.Network, 
                    Symbol = orderDto.FromTransfer.Symbol, 
                    Amount = orderDto.FromTransfer.Amount.ToString()
                },
                ToTransfer = new TransferInfoDto
                {
                    Network = orderDto.ToTransfer.Network, 
                }
            };

        var transferOrderMonitorGrain = GrainFactory.GetGrain<IWithdrawOrderMonitorGrain>(id);
        await transferOrderMonitorGrain.DoCallbackMonitor(TransferOrderMonitorDto.Create(dto, id, reason));
    }

    public async Task AddCheckOrder(WithdrawOrderDto orderDto)
    {
        await _orderStatusReminderGrain.AddReminder(GuidHelper.GenerateId(orderDto.Id.ToString(), OrderTypeEnum.Withdraw.ToString()));
    }
}