using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using NBitcoin;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWithdrawTimerGrain : IGrainWithGuidKey
{
    public Task AddToQuery(WithdrawOrderDto order);
    public Task<DateTime> GetLastCallBackTime();
}

public class WithdrawTimerGrain : Grain<WithdrawTimerState>, IWithdrawTimerGrain
{
    private readonly ILogger<WithdrawTimerGrain> _logger;
    private readonly TimerOptions _timerOptions;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;

    private const string SUCCESS = "success";
    private const string FAIL = "failed";
    private const string PENDING = "pending";

    public WithdrawTimerGrain(ILogger<WithdrawTimerGrain> logger, 
        IOptionsSnapshot<TimerOptions> timerOptions,
        ICoBoProvider coBoProvider, 
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<CoBoOptions> coBoOptions)
    {
        _logger = logger;
        _coBoProvider = coBoProvider;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _timerOptions = timerOptions.Value;
        _coBoOptions = coBoOptions;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("WithdrawTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync(cancellationToken);
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.DelaySeconds));

        State.WithdrawInfoMap ??= new Dictionary<Guid, WithdrawInfo>();
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("WithdrawTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(QueryCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    public async Task AddToQuery(WithdrawOrderDto order)
    {
        var callGrain = GrainFactory.GetGrain<IWithdrawOrderCallGrain>(order.Id);
        try
        {
            if (State.WithdrawInfoMap.ContainsKey(order.Id))
            {
                _logger.LogWarning("Order id {Id} exists in WithdrawTimerGrain state", order.Id);
                return;
            }

            var coin = GuidHelper.GenerateId(order.ToTransfer.Network, order.ToTransfer.Symbol);
            var netWorkInfo = _withdrawNetworkOptions.Value.NetworkInfos.FirstOrDefault(t =>
                t.Coin.Equals(coin, StringComparison.OrdinalIgnoreCase));
            var requestTime = DateTime.UtcNow.AddMinutes(1).ToUtcSeconds();
            var extraRequestTime = DateTime.UtcNow.AddMinutes(1).ToUtcSeconds();
            if (netWorkInfo != null)
            {
                _logger.LogDebug("WithdrawTimerGrain requestTime, {blockingTime}, {confirmNum}, {extraRequestTime}", 
                    netWorkInfo.BlockingTime, netWorkInfo.ConfirmNum, netWorkInfo.ExtraRequestTime);
                requestTime = DateTime.UtcNow.ToUtcSeconds() + (long)(netWorkInfo.BlockingTime * netWorkInfo.ConfirmNum);
                extraRequestTime = DateTime.UtcNow.ToUtcSeconds() + netWorkInfo.ExtraRequestTime;
            }
            
            await callGrain.AddOrGet(6);
            State.WithdrawInfoMap[order.Id] = new WithdrawInfo()
            {
                OrderId = order.Id,
                RequestTime = requestTime,
                ExtraRequestTime = extraRequestTime,
                LaterRequestTime = 0
            };
            await WriteStateAsync();
            await callGrain.AddOrGet(7);
        }
        catch (Exception e)
        {
            await callGrain.AddOrGet(8);
            _logger.LogError(e, "add to query error, orderId:{orderId}", order.Id);
        }
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(DateTime.Now);
    }

    private async Task QueryCallback(object state)
    {
        try
        {
            var total = State.WithdrawInfoMap.Count;
            _logger.LogDebug("WithdrawTimerGrain callback, Count={Count}", total);
            if (total < 1) return;

            var removed = 0;
            var removedItems = new List<KeyValuePair<Guid, WithdrawInfo>>();
            foreach (var withdrawItem in State.WithdrawInfoMap)
            {
                var currentTime = DateTime.UtcNow.ToUtcSeconds();
                if ((currentTime < withdrawItem.Value.ExtraRequestTime && currentTime < withdrawItem.Value.RequestTime)
                    || currentTime < withdrawItem.Value.LaterRequestTime)
                {
                    _logger.LogDebug(
                        "order confirm time not enough, orderId:{orderId}, currentTime:{currentTime}, requestTime:{requestTime}, " +
                        "remainingTime:{remainingTime}, extraRequestTime:{extraRequestTime}, laterRequestTime:{laterRequestTime},",
                        withdrawItem.Key, currentTime, withdrawItem.Value.RequestTime,
                        withdrawItem.Value.RequestTime - currentTime, withdrawItem.Value.ExtraRequestTime, withdrawItem.Value.LaterRequestTime);
                    continue;
                }

                var status = await HandleWithdraw(withdrawItem.Key, withdrawItem.Value, 
                    currentTime >= withdrawItem.Value.ExtraRequestTime && currentTime < withdrawItem.Value.RequestTime);
                if (status.Equals(ThirdPartOrderStatusEnum.Pending.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("withdraw order pending, orderId:{orderId}", withdrawItem.Key);
                    continue;
                }

                removedItems.Add(withdrawItem);
                removed++;
            }

            State.WithdrawInfoMap.RemoveAll(removedItems);
            await WriteStateAsync();

            if (total < 1)
            {
                _logger.LogDebug("WithdrawTimerGrain finish, count: {Removed}/{Total}", removed, total);
            }
            else
            {
                _logger.LogInformation("WithdrawTimerGrain finish, count: {Removed}/{Total}", removed, total);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "QueryCallback error. data:{data}", JsonConvert.SerializeObject(State.WithdrawInfoMap));
            HandleException();
        }
    }

    private async Task<string> HandleWithdraw(Guid orderId, WithdrawInfo withdrawInfo, bool isRange)
    {
        _logger.LogDebug("QueryWithdraw timer, {Time}, orderId:{orderId}, {isRange}", 
            DateTime.UtcNow.ToUtc8String(), orderId, isRange);

        var orderGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(orderId);
        var orderResult = await orderGrain.Get();
        if (orderResult?.Data == null)
        {
            _logger.LogWarning("order not exists, orderId:{orderId}", orderId);
            return ThirdPartOrderStatusEnum.Fail.ToString();
        }

        var order = (WithdrawOrderDto)orderResult.Data;
        if (isRange && order.ToTransfer.Status == PENDING)
        {
            _logger.LogWarning("order already pending, orderId:{orderId}", orderId);
            return PENDING;
        }

        var result = await GetWithdrawInfoByRequest(order.Id.ToString());
        if (result == null)
        {
            return PENDING;
        }

        order.ToTransfer.TxTime = result.LastTime;
        order.ToTransfer.Status = result.Status;
        order.ToTransfer.FromAddress = KeyMapping(result.SourceAddress);

        var extensionInfo = ExtensionBuilder.New()
            .Add(ExtensionKey.RequestId, result.RequestId)
            .Add(ExtensionKey.ToTransferTxId, result.TxId)
            .Build();

        switch (result.Status.ToLower())
        {
            case SUCCESS:
                order.ToTransfer.TxId = result.TxId;
                order.Status = OrderStatusEnum.ToTransferConfirmed.ToString();
                if (result.AbsCoBoFee.NotNullOrEmpty() && result.FeeCoin.NotNullOrEmpty())
                {
                    order.ThirdPartFee ??= new();
                    order.ThirdPartFee.Add(new FeeInfo(result.FeeCoin, result.FeeAmount, result.FeeDecimal,
                        FeeInfo.FeeName.CoBoFee));
                }
                order.ExtensionInfo ??= new Dictionary<string, string>();
                order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum, result.ConfirmedNum.ToString());
                await userWithdrawGrain.AddOrUpdateOrder(order, extensionInfo);
                break;
            case FAIL:
                order.ToTransfer.TxId = result.TxId;
                order.Status = OrderStatusEnum.ToTransferFailed.ToString();
                order.ExtensionInfo ??= new Dictionary<string, string>();
                order.ExtensionInfo.TryAdd(CommonConstant.WithdrawThirdPartErrorKey,
                    ThirdPartOrderStatusEnum.Fail.ToString());
                await userWithdrawGrain.AddOrUpdateOrder(order, extensionInfo);
                break;
            case PENDING:
                order.ToTransfer.TxId = result.TxId;
                order.ExtensionInfo ??= new Dictionary<string, string>();
                order.ExtensionInfo.AddOrReplace(ExtensionKey.ToConfirmedNum, result.ConfirmedNum.ToString());
                if (!isRange)
                {
                    withdrawInfo.LaterRequestTime =
                        DateTime.UtcNow.AddSeconds(CoBoConstant.WithdrawQueryInterval).ToUtcSeconds();
                }
                extensionInfo = ExtensionBuilder.New()
                    .Add(ExtensionKey.IsForward, Boolean.FalseString)
                    .Build();
                await userWithdrawGrain.AddOrUpdateOrder(order, extensionInfo);
                break;
            default:
                break;
        }

        return result.Status;
    }

    private async Task<WithdrawInfoDto> GetWithdrawInfoByRequest(string requestId)
    {
        try
        {
            var result = await _coBoProvider.GetWithdrawInfoByRequestIdAsync(requestId);
            _logger.LogInformation("withdraw result: {result}", JsonConvert.SerializeObject(result));
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "get withdraw info error, requestId:{requestId}", requestId);
            return null;
        }
    }

    private string KeyMapping(string key)
    {
        return _coBoOptions.Value.KeyMapping.GetValueOrDefault(key, key);
    }

    private void HandleException()
    {
        if (State.WithdrawInfoMap == null)
        {
            _logger.LogError("State.WithdrawInfoMap is null");
            State.WithdrawInfoMap ??= new Dictionary<Guid, WithdrawInfo>();
        }
    }
}