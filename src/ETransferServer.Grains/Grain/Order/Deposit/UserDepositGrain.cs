using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider;
using ETransferServer.Options;
using MassTransit;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface IUserDepositGrain : IGrainWithGuidKey
{
    Task AddOrUpdateOrder(DepositOrderDto orderDto, Dictionary<string, string> externalInfo = null);
}

/// <summary>
///     logic-for-each-stage-of-order-processing
/// </summary>
public partial class UserDepositGrain : Orleans.Grain, IAsyncObserver<DepositOrderDto>, IUserDepositGrain
{
    private const int MaxStreamSteps = 100;
    private int _currentSteps = 0;

    private readonly ILogger<UserDepositGrain> _logger;
    private IAsyncStream<DepositOrderDto> _orderChangeStream;

    private readonly IUserDepositProvider _userDepositProvider;
    private readonly IOrderStatusFlowProvider _orderStatusFlowProvider;

    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<DepositOptions> _depositOptions;

    private IUserDepositRecordGrain _recordGrain;
    private IOrderStatusFlowGrain _orderStatusFlowGrain;
    private IUserDepositTxTimerGrain _depositTxTimerGrain;
    private ISwapTxTimerGrain _swapTxTimerGrain;
    private IDepositOrderRetryTimerGrain _depositOrderRetryTimerGrain;
    private IOrderStatusReminderGrain _orderStatusReminderGrain;
    private ICoBoDepositQueryTimerGrain _depositQueryTimerGrain;
    private readonly IObjectMapper _objectMapper;
    private readonly IBus _bus;

    internal JsonSerializerSettings JsonSettings = JsonSettingsBuilder.New()
        .WithAElfTypesConverters()
        .WithCamelCasePropertyNamesResolver()
        .Build();

    public UserDepositGrain(IUserDepositProvider userDepositProvider,
        ILogger<UserDepositGrain> logger, IContractProvider contractProvider,
        IOptionsSnapshot<ChainOptions> chainOptions, IOptionsSnapshot<DepositOptions> depositOptions,
        IOrderStatusFlowProvider orderStatusFlowProvider,
        IObjectMapper objectMapper, 
        IBus bus)
    {
        _userDepositProvider = userDepositProvider;
        _logger = logger;
        _chainOptions = chainOptions;
        _depositOptions = depositOptions;
        _orderStatusFlowProvider = orderStatusFlowProvider;
        _contractProvider = contractProvider;
        _objectMapper = objectMapper;
        _bus = bus;
    }

    public override async Task OnActivateAsync()
    {
        await base.OnActivateAsync();

        // subscribe stream
        var streamProvider = GetStreamProvider(CommonConstant.StreamConstant.MessageStreamNameSpace);
        _orderChangeStream =
            streamProvider.GetStream<DepositOrderDto>(this.GetPrimaryKey(),
                _depositOptions.Value.OrderChangeTopic);
        await _orderChangeStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);

        // other grain
        _recordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(this.GetPrimaryKey());
        _orderStatusFlowGrain = GrainFactory.GetGrain<IOrderStatusFlowGrain>(this.GetPrimaryKey());
        _depositTxTimerGrain =
            GrainFactory.GetGrain<IUserDepositTxTimerGrain>(GuidHelper.UniqGuid(nameof(IUserDepositTxTimerGrain)));
        _swapTxTimerGrain =
            GrainFactory.GetGrain<ISwapTxTimerGrain>(GuidHelper.UniqGuid(nameof(ISwapTxTimerGrain)));
        _depositOrderRetryTimerGrain =
            GrainFactory.GetGrain<IDepositOrderRetryTimerGrain>(
                GuidHelper.UniqGuid(nameof(IDepositOrderRetryTimerGrain)));
        _orderStatusReminderGrain = 
            GrainFactory.GetGrain<IOrderStatusReminderGrain>(
                GuidHelper.UniqGuid(nameof(IOrderStatusReminderGrain)));
        _depositQueryTimerGrain = 
            GrainFactory.GetGrain<ICoBoDepositQueryTimerGrain>(
            GuidHelper.UniqGuid(nameof(ICoBoDepositQueryTimerGrain)));
    }

    public async Task AddOrUpdateOrder(DepositOrderDto orderDto, Dictionary<string, string> externalInfo = null)
    {
        _logger.LogInformation(
            "AddOrUpdateOrder, orderDto: {orderDto}, externalInfo: {externalInfo}",
            JsonConvert.SerializeObject(orderDto), JsonConvert.SerializeObject(externalInfo));

        // save deposit order to Grain
        var res = await _recordGrain.CreateOrUpdateAsync(orderDto);
        AssertHelper.IsTrue(res.Success, "save order data error, orderId = {Id}", orderDto.Id);

        // save order flow
        var orderFlowRes = await _orderStatusFlowGrain.AddAsync(orderDto.Status, externalInfo);

        // save deposit order to ES
        await _userDepositProvider.AddOrUpdateSync(res.Value);

        // save order flow to ES
        await _orderStatusFlowProvider.AddOrUpdate(orderDto.Id, orderFlowRes);

        if (IsForward(orderDto, externalInfo)) return;
        // push order to stream
        _logger.LogInformation("push to stream, type:deposit, orderId:{orderId}, status:{status}", orderDto.Id,
            orderDto.Status);
        await _orderChangeStream.OnNextAsync(orderDto);
    }
    
    private bool IsForward(DepositOrderDto orderDto, Dictionary<string, string> externalInfo)
    {
        var status = Enum.Parse<OrderStatusEnum>(orderDto.Status);
        if (status == OrderStatusEnum.ToTransferring && !externalInfo.IsNullOrEmpty() &&
            externalInfo.ContainsKey(ExtensionKey.IsForward))
        {
            var result = bool.TryParse(externalInfo[ExtensionKey.IsForward], out var isForward);
            _logger.LogInformation("IsForward, type:deposit, orderId:{OrderId}, isForward:{isForward}",
                orderDto.Id, isForward);
            return result && !isForward;
        }

        return false;
    }
    
    public async Task AddCheckOrder(DepositOrderDto orderDto)
    {
        await _orderStatusReminderGrain.AddReminder(GuidHelper.GenerateId(orderDto.Id.ToString(), OrderTypeEnum.Deposit.ToString()));
    }
}