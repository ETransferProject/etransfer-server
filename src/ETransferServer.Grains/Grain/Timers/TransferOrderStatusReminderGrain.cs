using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Runtime;
using Orleans.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;

namespace ETransferServer.Grains.Grain.Timers;

public interface ITransferOrderStatusReminderGrain : IGrainWithGuidKey
{
    Task AddReminder(String id);
}

public class TransferOrderStatusReminderGrain : Orleans.Grain, ITransferOrderStatusReminderGrain, IRemindable
{
    private const string AlarmNotifyTemplate = "TransferCallbackAlarm";
    private const string Reason = "The order has been confirmed but no callback has been received from cobo.";
    private readonly ILogger<OrderStatusReminderGrain> _logger;
    private readonly IReminderRegistry _reminderRegistry;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;
    private readonly Dictionary<string, int> _reminderCountMap = new();
    private const int RetryCountMax = 3;

    public TransferOrderStatusReminderGrain(IReminderRegistry reminderRegistry, ILogger<OrderStatusReminderGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions, IEnumerable<INotifyProvider> notifyProvider)
    {
        _reminderRegistry = reminderRegistry;
        _logger = logger;
        _timerOptions = timerOptions;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }

    public async Task StartReminder(String id)
    {
        _logger.LogDebug("TransferOrderStatusReminderGrain Startup dueTimeSec={Due}, periodSec={Per}",
            _timerOptions.Value.TransferOrderStatusReminder.DelaySeconds,
            _timerOptions.Value.TransferOrderStatusReminder.PeriodSeconds);
        await _reminderRegistry.RegisterOrUpdateReminder(
            this.GetGrainId(),
            reminderName: id,
            dueTime: TimeSpan.FromSeconds(_timerOptions.Value.TransferOrderStatusReminder.DelaySeconds),
            period: TimeSpan.FromSeconds(_timerOptions.Value.TransferOrderStatusReminder.PeriodSeconds));
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        await CheckOrder(reminderName);
    }

    public async Task CheckOrder(String reminderName)
    {
        var cancel = false;
        try
        {
            _reminderCountMap.GetOrAdd(reminderName, _ => new());
            _logger.LogInformation("TransferOrderStatusReminderGrain CheckOrder reminderName={reminderName}", reminderName);
            var orderId = Guid.Parse(reminderName);
            var order = await GetOrder(orderId);

            _logger.LogInformation("TransferOrderStatusReminderGrain CheckOrder orderId={orderId}", order.Id);
            if (order.ThirdPartOrderId.IsNullOrEmpty() && !order.FromTransfer.TxId.IsNullOrEmpty() &&
                (!order.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus) ||
                 order.ExtensionInfo[ExtensionKey.SubStatus] !=
                 OrderOperationStatusEnum.UserTransferRejected.ToString()))
            {
                cancel = await SendNotifyAsync(order);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransferOrderStatusReminderGrain CheckOrder Exception reminderName={reminderName}",
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
        var grainReminder = await _reminderRegistry.GetReminder(this.GetGrainId(), reminderName);
        await _reminderRegistry.UnregisterReminder(this.GetGrainId(), grainReminder);
        _reminderCountMap.Remove(reminderName);
    }

    private async Task<bool> SendNotifyAsync(BaseOrderDto order)
    {
        _logger.LogInformation("TransferOrderStatusReminderGrain SendNotifyAsync orderId={orderId}", order.Id);
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");

        var notifyRequest = new NotifyRequest
        {
            Template = AlarmNotifyTemplate,
            Params = new Dictionary<string, string>
            {
                [Keys.OrderType] = OrderTypeEnum.Transfer.ToString(),
                [Keys.OrderId] = order.Id.ToString(),
                [Keys.FromNetwork] = order.FromTransfer.Network,
                [Keys.ToNetwork] = order.ToTransfer.Network,
                [Keys.Amount] = order.FromTransfer.Amount.ToString(),
                [Keys.Symbol] = order.FromTransfer.Symbol,
                [Keys.Reason] = Reason
            }
        };
        return await provider.SendNotifyAsync(notifyRequest);
    }

    private async Task<BaseOrderDto> GetOrder(Guid orderId)
    {
        var order = new BaseOrderDto
        {
            Id = orderId
        };

        try
        {
            var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
            order = (await recordGrain.Get())?.Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "TransferOrderStatusReminderGrain Exception orderId={orderId}", orderId);
        }

        AssertHelper.NotNull(order, "order is null");
        return order;
    }

    private static class Keys
    {
        public const string OrderType = "orderType";
        public const string OrderId = "orderId";
        public const string FromNetwork = "fromNetwork";
        public const string ToNetwork = "toNetwork";
        public const string Amount = "amount";
        public const string Symbol = "symbol";
        public const string Reason = "reason";
    }
}