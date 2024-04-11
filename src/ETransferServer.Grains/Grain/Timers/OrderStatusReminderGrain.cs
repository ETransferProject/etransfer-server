using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Order.Withdraw;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;

namespace ETransferServer.Grains.Grain.Timers;

public interface IOrderStatusReminderGrain : IGrainWithGuidKey
{
    Task AddReminder(String id);
}

public class OrderStatusReminderGrain : Orleans.Grain, IOrderStatusReminderGrain, IRemindable
{
    private const string AlarmNotifyTemplate = "OrderStatusAlarm";
    private readonly ILogger<OrderStatusReminderGrain> _logger;
    private readonly IReminderRegistry _reminderRegistry;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;
    private readonly Dictionary<string, int> _reminderCountMap = new();
    private const int RetryCountMax = 3;

    public OrderStatusReminderGrain(IReminderRegistry reminderRegistry, ILogger<OrderStatusReminderGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions, IEnumerable<INotifyProvider> notifyProvider)
    {
        _reminderRegistry = reminderRegistry;
        _logger = logger;
        _timerOptions = timerOptions;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }

    public async Task StartReminder(String id)
    {
        _logger.LogDebug("OrderStatusReminderGrain Startup dueTimeSec={Due}, periodSec={Per}",
            _timerOptions.Value.OrderStatusReminder.DelaySeconds,
            _timerOptions.Value.OrderStatusReminder.PeriodSeconds);
        await _reminderRegistry.RegisterOrUpdateReminder(
            reminderName: id,
            dueTime: TimeSpan.FromSeconds(_timerOptions.Value.OrderStatusReminder.DelaySeconds),
            period: TimeSpan.FromSeconds(_timerOptions.Value.OrderStatusReminder.PeriodSeconds));
    }

    public Task ReceiveReminder(string reminderName, TickStatus status)
    {
        CheckOrder(reminderName);
        return Task.CompletedTask;
    }

    public async Task CheckOrder(String reminderName)
    {
        var cancel = false;
        try
        {
            _reminderCountMap.GetOrAdd(reminderName, _ => new());
            _logger.LogInformation("OrderStatusReminderGrain CheckOrder reminderName={reminderName}", reminderName);
            var nameSplit = reminderName.Split(CommonConstant.Underline);
            var orderId = Guid.Parse(nameSplit[0]);
            var orderType = nameSplit.Length > 1 ? nameSplit[1] : "";
            var order = await GetOrder(orderType, orderId);

            _logger.LogInformation("OrderStatusReminderGrain CheckOrder orderId={orderId}", order.Id);
            cancel = OrderStatusEnum.Finish.ToString().Equals(order.Status) || await SendNotifyAsync(order, orderType);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "OrderStatusReminderGrain CheckOrder Exception reminderName={reminderName}",
                reminderName);
        }
        finally
        {
            if (++_reminderCountMap[reminderName] >= RetryCountMax || cancel)
            {
                await CancelReminder(reminderName);
            }
        }
    }

    public async Task AddReminder(String id)
    {
        await StartReminder(id);
    }

    private async Task CancelReminder(string reminderName)
    {
        var grainReminder = await _reminderRegistry.GetReminder(reminderName);
        await _reminderRegistry.UnregisterReminder(grainReminder);
        _reminderCountMap.Remove(reminderName);
    }

    private async Task<bool> SendNotifyAsync(BaseOrderDto order, string type)
    {
        _logger.LogInformation("OrderStatusReminderGrain SendNotifyAsync orderId={orderId} type={type}", order.Id, type);
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");
        var createTime = 0l;
        if (order.CreateTime.HasValue)
        { 
            createTime = order.CreateTime.Value;
        }

        var toTransfer = order.ToTransfer;
        var fromTransfer = order.FromTransfer;
        var notifyRequest = new NotifyRequest
        {
            Template = AlarmNotifyTemplate,
            Params = new Dictionary<string, string>
            {
                [Keys.UserId] = order.UserId.ToString(),
                [Keys.OrderType] = order.OrderType,
                [Keys.OrderId] = order.Id.ToString(),
                [Keys.CreateTime] = TimeHelper.GetDateTimeFromTimeStamp(createTime).ToUtc8String(),
                [Keys.TxId] = toTransfer?.TxId,
                [Keys.Reason] = order.ExtensionInfo.IsNullOrEmpty() ? "" : order.ExtensionInfo.First().Value,

                [Keys.AmountFrom] = GetAmount(fromTransfer),
                [Keys.AmountTo] = GetAmount(toTransfer),

                [Keys.NetworkFrom] = OrderTypeEnum.Withdraw.ToString().Equals(order.OrderType)
                    ? fromTransfer?.ChainId
                    : fromTransfer?.Network,
                [Keys.NetworkTo] = OrderTypeEnum.Withdraw.ToString().Equals(order.OrderType)
                    ? toTransfer?.Network
                    : toTransfer?.ChainId,

                [Keys.FromAddressFrom] = fromTransfer?.FromAddress,
                [Keys.FromAddressTo] = fromTransfer?.ToAddress,

                [Keys.ToAddressFrom] = toTransfer?.FromAddress,
                [Keys.ToAddressTo] = toTransfer?.ToAddress,
            }
        };
        return await provider.SendNotifyAsync(notifyRequest);
    }

    private async Task<BaseOrderDto> GetOrder(string orderType, Guid orderId)
    {
        var order = new BaseOrderDto
        {
            Id = orderId
        };

        try
        {
            var orderTypeEnum = Enum.Parse<OrderTypeEnum>(orderType);
            switch (orderTypeEnum)
            {
                case OrderTypeEnum.Withdraw:
                    var withdrawRecordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
                    order = (await withdrawRecordGrain.GetAsync())?.Value;
                    break;
                case OrderTypeEnum.Deposit:
                    var depositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
                    order = (await depositRecordGrain.GetAsync())?.Value;
                    break;
                default:
                    _logger.LogInformation("OrderStatusReminderGrain reminderName not right orderType={orderType} orderId={orderId}", orderType, orderId);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,"OrderStatusReminderGrain Exception orderType={orderType} orderId={orderId}", orderType, orderId);
        }

        AssertHelper.NotNull(order, "order is null");
        return order;
    }

    private static string GetAmount(TransferInfo transferInfo)
    {
        return transferInfo != null ? string.Join(CommonConstant.Space, transferInfo.Amount, transferInfo.Symbol) : "";
    }

    private static class Keys
    {
        public const string UserId = "userId";
        public const string OrderType = "orderType";
        public const string OrderId = "orderId";
        public const string CreateTime = "createTime";
        public const string TxId = "txId";
        public const string Reason = "reason";
        public const string AmountFrom = "amountFrom";
        public const string AmountTo = "amountTo";

        //from
        public const string NetworkFrom = "networkFrom";
        public const string FromAddressFrom = "fromAddressFrom";
        public const string FromAddressTo = "fromAddressTo";

        //to
        public const string NetworkTo = "networkTo";
        public const string ToAddressFrom = "toAddressFrom";
        public const string ToAddressTo = "toAddressTo";
    }
}