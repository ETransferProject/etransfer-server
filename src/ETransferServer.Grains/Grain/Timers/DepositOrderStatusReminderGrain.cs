using ETransferServer.Common;
using ETransferServer.Dtos.Notify;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Deposit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace ETransferServer.Grains.Grain.Timers;

public interface IDepositOrderStatusReminderGrain : IGrainWithGuidKey
{
    Task AddReminder(String id);
}

public class DepositOrderStatusReminderGrain : Orleans.Grain, IDepositOrderStatusReminderGrain, IRemindable
{
    private const string DepositOrderPendingAlarmTemplate = "DepositOrderPendingAlarm";
    private const string DepositOrderLostAlarmTemplate = CommonConstant.DepositOrderLostAlarm;
    // private const string DepositOrderCoinNotSupportAlarmTemplate = CommonConstant.DepositOrderCoinNotSupportAlarm;
    private readonly ILogger<DepositOrderStatusReminderGrain> _logger;
    private readonly IReminderRegistry _reminderRegistry;
    private readonly IOptionsSnapshot<TimerOptions> _timerOptions;
    private readonly IUserDepositProvider _userDepositProvider;
    private readonly Dictionary<string, INotifyProvider> _notifyProvider;
    private readonly Dictionary<string, int> _reminderCountMap = new();
    private const int RetryCountMax = 3;

    public DepositOrderStatusReminderGrain(IReminderRegistry reminderRegistry,
        ILogger<DepositOrderStatusReminderGrain> logger,
        IOptionsSnapshot<TimerOptions> timerOptions,
        IUserDepositProvider userDepositProvider,
        IEnumerable<INotifyProvider> notifyProvider)
    {
        _reminderRegistry = reminderRegistry;
        _logger = logger;
        _timerOptions = timerOptions;
        _userDepositProvider = userDepositProvider;
        _notifyProvider = notifyProvider.ToDictionary(p => p.NotifyType().ToString());
    }

    public async Task StartReminder(String id)
    {
        _logger.LogDebug("DepositOrderStatusReminderGrain Startup dueTimeSec={Due}, periodSec={Per}",
            _timerOptions.Value.DepositOrderStatusReminder.DelaySeconds,
            _timerOptions.Value.DepositOrderStatusReminder.PeriodSeconds);
        
        var existingReminder = await _reminderRegistry.GetReminder(id);
        if (existingReminder == null)
        {
            await _reminderRegistry.RegisterOrUpdateReminder(
                reminderName: id,
                dueTime: TimeSpan.FromSeconds(_timerOptions.Value.DepositOrderStatusReminder.DelaySeconds),
                period: TimeSpan.FromSeconds(_timerOptions.Value.DepositOrderStatusReminder.PeriodSeconds));
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        await CheckDepositOrder(reminderName);
    }

    public async Task CheckDepositOrder(String reminderName)
    {
        var cancel = false;
        try
        {
            _reminderCountMap.GetOrAdd(reminderName, _ => new());
            _logger.LogInformation("DepositOrderStatusReminderGrain CheckOrder reminderName={reminderName}",
                reminderName);
            var nameSplit = reminderName.Split(CommonConstant.Underline);
            var transactionId = ParseNameSplit(nameSplit[0]).transactionId;
            var orderId = nameSplit.Length > 1 ? Guid.Parse(nameSplit[1]) : Guid.Empty;
            if (orderId == Guid.Empty)
            {
                _logger.LogInformation("DepositOrderStatusReminderGrain CheckOrder txId={txId}", transactionId);
                cancel = await _userDepositProvider.ExistSync(new DepositOrderDto { ThirdPartOrderId = transactionId })
                         || await SendNotifyAsync(transactionId);
            }
            else
            {
                var order = await GetDepositOrder(transactionId, orderId);
                _logger.LogInformation("DepositOrderStatusReminderGrain CheckOrder orderId={orderId}", order.Id);
                cancel = OrderStatusEnum.Finish.ToString().Equals(order.Status) || await SendNotifyAsync(order);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "DepositOrderStatusReminderGrain Exception, reminderName={reminderName}",
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
    
    private (string transactionId, string templateName) ParseNameSplit(string nameSplit)
    {
        string transactionId;
        string templateName;

        if (nameSplit.Contains(CommonConstant.Colon))
        {
            var parts = nameSplit.Split(CommonConstant.Colon);
            transactionId = parts[0];
            templateName = parts[1];
        }
        else
        {
            transactionId = nameSplit;
            templateName = DepositOrderLostAlarmTemplate;
        }

        return (transactionId, templateName);
    }
    
    

    public async Task AddReminder(String id)
    {
        await StartReminder(id);
    }

    private async Task CancelReminder(string reminderName)
    {
        _logger.LogInformation("DepositOrderStatusReminderGrain UnregisterReminder, reminderName={reminderName}", reminderName);
        var grainReminder = await _reminderRegistry.GetReminder(reminderName);
        await _reminderRegistry.UnregisterReminder(grainReminder);
        _reminderCountMap.Remove(reminderName);
    }

    private async Task<bool> SendNotifyAsync(string transactionId, string template = DepositOrderLostAlarmTemplate)
    {
        _logger.LogInformation("DepositOrderStatusReminderGrain SendNotifyAsync txId={txId}", transactionId);
        var providerExists = _notifyProvider.TryGetValue(NotifyTypeEnum.FeiShuGroup.ToString(), out var provider);
        AssertHelper.IsTrue(providerExists, "Provider not found");

        var coBoDepositGrain = GrainFactory.GetGrain<ICoBoDepositGrain>(transactionId);
        var coBoTransaction = await coBoDepositGrain.Get();

        var notifyRequest = new NotifyRequest
        {
            Template = template,
            Params = new Dictionary<string, string>
            {
                [Keys.OrderType] = OrderTypeEnum.Deposit.ToString(),
                [Keys.Message] = coBoTransaction == null
                    ? string.Empty
                    : JsonConvert.SerializeObject(coBoTransaction)
                        .Replace("\"", string.Empty)
            }
        };
        return await provider.SendNotifyAsync(notifyRequest);
    }

    private async Task<bool> SendNotifyAsync(BaseOrderDto order)
    {
        _logger.LogInformation("DepositOrderStatusReminderGrain SendNotifyAsync orderId={orderId}", order.Id);
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
            Template = DepositOrderPendingAlarmTemplate,
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

                [Keys.NetworkFrom] = fromTransfer?.Network,
                [Keys.NetworkTo] = toTransfer?.ChainId,

                [Keys.FromAddressFrom] = fromTransfer?.FromAddress,
                [Keys.FromAddressTo] = fromTransfer?.ToAddress,

                [Keys.ToAddressFrom] = toTransfer?.FromAddress,
                [Keys.ToAddressTo] = toTransfer?.ToAddress,
                [Keys.Status] = order.Status
            }
        };
        return await provider.SendNotifyAsync(notifyRequest);
    }

    private async Task<BaseOrderDto> GetDepositOrder(string transactionId, Guid orderId)
    {
        var order = new BaseOrderDto
        {
            Id = orderId
        };

        try
        {
            var depositRecordGrain = GrainFactory.GetGrain<IUserDepositRecordGrain>(orderId);
            order = (await depositRecordGrain.GetAsync())?.Value;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "DepositOrderStatusReminderGrain Exception, orderId={orderId}", orderId);
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
        public const string Status = "status";
        public const string Message = "message";
    }
}