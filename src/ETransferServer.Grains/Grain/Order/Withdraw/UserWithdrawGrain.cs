using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider;
using ETransferServer.Options;

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

    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly IOptionsMonitor<WithdrawOptions> _withdrawOptions;
    private readonly IOptionsMonitor<WithdrawNetworkOptions> _withdrawNetworkOptions;

    private IUserWithdrawRecordGrain _recordGrain;
    private IOrderStatusFlowGrain _orderStatusFlowGrain;
    private IUserWithdrawTxTimerGrain _withdrawTimerGrain;
    private IWithdrawTimerGrain _withdrawQueryTimerGrain;
    private IOrderStatusReminderGrain _orderStatusReminderGrain;

    private readonly IContractProvider _contractProvider;
    private readonly IUserWithdrawProvider _userWithdrawProvider;
    private readonly IOrderStatusFlowProvider _orderStatusFlowProvider;


    private const int MaxStreamSteps = 100;
    private int _currentSteps = 0;

    public UserWithdrawGrain(IUserWithdrawProvider userWithdrawProvider,
        ILogger<UserWithdrawGrain> logger, IOptionsMonitor<ChainOptions> chainOptions,
        IOptionsMonitor<WithdrawOptions> withdrawOptions, IContractProvider contractProvider,
        IOrderStatusFlowProvider orderStatusFlowProvider,
        IOptionsMonitor<WithdrawNetworkOptions> withdrawNetworkOptions)
    {
        _userWithdrawProvider = userWithdrawProvider;
        _logger = logger;
        _chainOptions = chainOptions;
        _withdrawOptions = withdrawOptions;
        _contractProvider = contractProvider;
        _orderStatusFlowProvider = orderStatusFlowProvider;
        _withdrawNetworkOptions = withdrawNetworkOptions;
    }

    public override async Task OnActivateAsync()
    {
        await base.OnActivateAsync();

        // subscribe stream
        var streamProvider = GetStreamProvider(CommonConstant.StreamConstant.MessageStreamNameSpace);
        _orderChangeStream =
            streamProvider.GetStream<WithdrawOrderDto>(this.GetPrimaryKey(),
                _withdrawOptions.CurrentValue.OrderChangeTopic);
        await _orderChangeStream.SubscribeAsync(OnNextAsync);

        // other grain
        _recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(this.GetPrimaryKey());
        _orderStatusFlowGrain = GrainFactory.GetGrain<IOrderStatusFlowGrain>(this.GetPrimaryKey());

        _withdrawTimerGrain =
            GrainFactory.GetGrain<IUserWithdrawTxTimerGrain>(
                GuidHelper.UniqGuid(nameof(IUserWithdrawTxTimerGrain)));
        _withdrawQueryTimerGrain =
            GrainFactory.GetGrain<IWithdrawTimerGrain>(
                GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
        _orderStatusReminderGrain = 
            GrainFactory.GetGrain<IOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IOrderStatusReminderGrain)));
    }

    public async Task<WithdrawOrderDto> CreateOrder(WithdrawOrderDto withdrawOrderDto)
    {
        AssertHelper.NotNull(withdrawOrderDto, "Empty withdraw order");
        AssertHelper.IsTrue(withdrawOrderDto.OrderType == OrderTypeEnum.Withdraw.ToString(),
            "Invalid order type {OrderType}", withdrawOrderDto.OrderType);
        AssertHelper.NotNull(withdrawOrderDto.FromTransfer, "Invalid from transfer info");
        AssertHelper.NotEmpty(withdrawOrderDto.FromTransfer.ChainId, "Invalid from transfer chainId");
        AssertHelper.NotEmpty(withdrawOrderDto.RawTransaction, "Invalid rawTransaction");
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer, "Invalid toTransfer");
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.Network, "Invalid toTransfer network");
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.ToAddress, "Invalid toTransfer toAddress");
        AssertHelper.NotNull(withdrawOrderDto.ToTransfer.Symbol, "Invalid toTransfer symbol");
        AssertHelper.IsTrue(withdrawOrderDto.ToTransfer.Amount > 0, "Invalid toTransfer amount");
        
        var coinInfo = _withdrawNetworkOptions.CurrentValue.GetNetworkInfo(withdrawOrderDto.ToTransfer.Network,
            withdrawOrderDto.ToTransfer.Symbol);
        AssertHelper.IsTrue(coinInfo.Decimal >= 0, "Invalid withdraw coin {Coin} decimal: {Decimals}", 
            coinInfo.Coin, coinInfo.Decimal);
        
        withdrawOrderDto.Id = this.GetPrimaryKey();
        withdrawOrderDto.Status = OrderStatusEnum.Created.ToString();
        withdrawOrderDto.FromTransfer.Network = CommonConstant.Network.AElf;
        return await AddOrUpdateOrder(withdrawOrderDto);
    }

    public async Task<WithdrawOrderDto> AddOrUpdateOrder(WithdrawOrderDto orderDto,
        Dictionary<string, string> externalInfo = null)
    {
        // save withdraw order to Grain
        var res = await _recordGrain.AddOrUpdateAsync(orderDto);
        AssertHelper.IsTrue(res.Success, "save order data error, orderId = {Id}", orderDto.Id);

        // save order status flow
        var orderFlowRes = await _orderStatusFlowGrain.AddAsync(orderDto.Status, externalInfo);

        // save withdraw order to ES
        await _userWithdrawProvider.AddOrUpdateSync(orderDto);

        // save order flow to ES
        await _orderStatusFlowProvider.AddOrUpdate(orderDto.Id, orderFlowRes);

        // push order to stream
        _logger.LogInformation("push to stream, type:withdraw, orderId:{OrderId}, status:{Status}",
            orderDto.Id, orderDto.Status);
        await _orderChangeStream.OnNextAsync(orderDto);

        return res.Value;
    }
    
    public async Task AddCheckOrder(WithdrawOrderDto orderDto)
    {
        await _orderStatusReminderGrain.AddReminder(orderDto.Id +  "_" + OrderStatusReminderGrain.Type.Deposit);
    }
}