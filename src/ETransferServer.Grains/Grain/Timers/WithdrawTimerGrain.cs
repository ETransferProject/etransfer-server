using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using NBitcoin;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWithdrawTimerGrain : IGrainWithGuidKey
{
    public Task AddToRequest(WithdrawOrderDto order);
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
    private const int RETRYCOUNT = 4;

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

    public override async Task OnActivateAsync()
    {
        _logger.LogDebug("WithdrawTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync();
        await StartTimer(TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.DelaySeconds));

        await StartWithRequestTimer(TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.DelaySeconds));

        State.WithdrawInfoMap ??= new Dictionary<Guid, WithdrawInfo>();
        State.WithdrawRequestMap ??= new Dictionary<Guid, WithdrawRequestInfo>();
    }

    private Task StartTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("WithdrawTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(QueryCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private Task StartWithRequestTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("WithdrawTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(RequestCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task RequestCallback(object state)
    {
        try
        {
            var total = State.WithdrawRequestMap.Count;
            _logger.LogDebug("WithdrawTimerGrain request callback, Count={Count}", total);
            if (total == 0)
            {
                return;
            }

            var removed = 0;
            var removedItems = new List<KeyValuePair<Guid, WithdrawRequestInfo>>();

            foreach (var withdrawRequest in State.WithdrawRequestMap)
            {
                var orderGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(withdrawRequest.Key);
                var orderResult = await orderGrain.Get();
                if (orderResult?.Data == null)
                {
                    _logger.LogWarning("order not exists, orderId:{orderId}", withdrawRequest.Key);
                    continue;
                }

                var orderDto = (WithdrawOrderDto)orderResult.Data;
                await Withdraw(orderDto, withdrawRequest);
                if (withdrawRequest.Value.Success)
                {
                    removed++;
                    removedItems.Add(withdrawRequest);
                    continue;
                }

                await HandleFailRequest(orderDto, withdrawRequest);
                if (withdrawRequest.Value.RetryCount >= RETRYCOUNT)
                {
                    removed++;
                    removedItems.Add(withdrawRequest);
                }
                else
                {
                    withdrawRequest.Value.RetryCount++;
                }
            }

            State.WithdrawRequestMap.RemoveAll(removedItems);
            _logger.LogInformation("WithdrawTimerGrain finish, count: {Removed}/{Total}", removed, total);

            await WriteStateAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "RequestCallback error, data:{data}",
                JsonConvert.SerializeObject(State.WithdrawRequestMap));
            HandleException();
        }
    }

    public async Task AddToRequest(WithdrawOrderDto order)
    {
        try
        {
            var orderGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(order.Id);
            var orderResult = await orderGrain.Get();
            if (orderResult?.Data == null)
            {
                _logger.LogWarning("add to request fail, order not exists, orderId:{orderId}", order.Id);
                return;
            }

            var result = await Withdraw(order);

            if (result.success)
            {
                order.Status = OrderStatusEnum.ToTransferring.ToString();
                await UpdateOrderStatus(order);
                return;
            }

            if (State.WithdrawRequestMap.ContainsKey(order.Id))
            {
                _logger.LogWarning("add to request fail, order id {Id} exists in WithdrawTimerGrain state", order.Id);
                return;
            }

            var errorKey = GetWithdrawErrorKey();
            State.WithdrawRequestMap[order.Id] = new WithdrawRequestInfo()
            {
                OrderId = order.Id,
                RetryCount = 1,
                Success = false,
                ErrorDic = new Dictionary<string, string>()
                {
                    [errorKey] = result.message
                }
            };
            await WriteStateAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "add to request error, orderId:{orderId}", order.Id);
        }
    }

    public async Task AddToQuery(WithdrawOrderDto order)
    {
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
                requestTime = DateTime.UtcNow.ToUtcSeconds() + netWorkInfo.BlockingTime * netWorkInfo.ConfirmNum;
                extraRequestTime = DateTime.UtcNow.ToUtcSeconds() + netWorkInfo.ExtraRequestTime;
            }

            State.WithdrawInfoMap[order.Id] = new WithdrawInfo()
            {
                OrderId = order.Id,
                RequestTime = requestTime,
                ExtraRequestTime = extraRequestTime,
            };
            await WriteStateAsync();
        }
        catch (Exception e)
        {
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
                if (currentTime > withdrawItem.Value.ExtraRequestTime && currentTime < withdrawItem.Value.RequestTime)
                {
                    _logger.LogDebug(
                        "order confirm time not enough, orderId:{orderId}, currentTime:{currentTime}, requestTime:{requestTime}, remainingTime:{remainingTime}, extraRequestTime:{extraRequestTime}",
                        withdrawItem.Key, currentTime, withdrawItem.Value.RequestTime,
                        withdrawItem.Value.RequestTime - currentTime, withdrawItem.Value.ExtraRequestTime);
                    continue;
                }

                var status = await HandleWithdraw(withdrawItem.Key, withdrawItem.Value);
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

    private async Task<string> HandleWithdraw(Guid orderId, WithdrawInfo withdrawInfo)
    {
        _logger.LogDebug("QueryWithdraw timer, {Time}, orderId:{orderId}", DateTime.UtcNow.ToUtc8String(),
            orderId);

        var orderGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(orderId);
        var orderResult = await orderGrain.Get();
        if (orderResult?.Data == null)
        {
            _logger.LogWarning("order not exists, orderId:{orderId}", orderId);
            return ThirdPartOrderStatusEnum.Fail.ToString();
        }

        var order = (WithdrawOrderDto)orderResult.Data;

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
                withdrawInfo.RequestTime =
                    DateTime.UtcNow.AddSeconds(CoBoConstant.WithdrawQueryInterval).ToUtcSeconds();
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

    private async Task<(bool success, string message)> Withdraw(WithdrawOrderDto orderDto)
    {
        try
        {
            var coinInfo = _withdrawNetworkOptions.Value.GetNetworkInfo(orderDto.ToTransfer.Network, orderDto.ToTransfer.Symbol);
            var amount = BigCalculationHelper.CalculateAmount(orderDto.ToTransfer.Amount, coinInfo.Decimal);
            var requestDto = new WithdrawRequestDto
            {
                Coin = coinInfo.Coin,
                RequestId = orderDto.Id.ToString(),
                Address = orderDto.ToTransfer.ToAddress,
                Amount = amount
            };

            orderDto.ExtensionInfo ??= new Dictionary<string, string>();
            if (orderDto.ExtensionInfo.ContainsKey(ExtensionKey.Memo))
            {
                requestDto.Memo = orderDto.ExtensionInfo[ExtensionKey.Memo];
            }
            orderDto.ThirdPartServiceName = ThirdPartServiceNameEnum.Cobo.ToString();
            var response = await _coBoProvider.WithdrawAsync(requestDto);

            if (response == null)
            {
                _logger.LogWarning("withdraw order {Id} stream error, request to withdraw fail.", orderDto.Id);
                return (false, CoBoConstant.ResponseNull);
            }
            else
            {
                _logger.LogInformation("withdraw order {Id} stream, request to withdraw success.",
                    orderDto.Id);
                return (true, string.Empty);
            }
        }
        catch (Exception e)
        {
            if (e is UserFriendlyException { Code: CommonConstant.ThirdPartResponseCode.DuplicateRequest })
            {
                _logger.LogInformation("duplicate withdraw requestId:{requestId}", orderDto.Id);
                return (true, string.Empty);
            }

            try
            {
                _logger.LogError(e, "withdraw order {Id} stream error, request to withdraw error.", orderDto.Id);
                return (false, e.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(CoBoConstant.ResponseParseError);
                return (false, CoBoConstant.ResponseParseError);
            }
        }
    }

    private async Task Withdraw(WithdrawOrderDto orderDto,
        KeyValuePair<Guid, WithdrawRequestInfo> withdrawRequest)
    {
        var extensionKey = GetWithdrawErrorKey(withdrawRequest.Value.RetryCount);
        try
        {
            var coinInfo = _withdrawNetworkOptions.Value.GetNetworkInfo(orderDto.ToTransfer.Network, orderDto.ToTransfer.Symbol);
            var amount = BigCalculationHelper.CalculateAmount(orderDto.ToTransfer.Amount, coinInfo.Decimal);
            var requestDto = new WithdrawRequestDto
            {
                Coin = coinInfo.Coin,
                RequestId = orderDto.Id.ToString(),
                Address = orderDto.ToTransfer.ToAddress,
                Amount = amount
            };

            orderDto.ExtensionInfo ??= new Dictionary<string, string>();
            if (orderDto.ExtensionInfo.ContainsKey(ExtensionKey.Memo))
            {
                requestDto.Memo = orderDto.ExtensionInfo[ExtensionKey.Memo];
            }
            var response = await _coBoProvider.WithdrawAsync(requestDto);

            if (response == null)
            {
                _logger.LogWarning("withdraw order {Id} stream error, request to withdraw fail.", orderDto.Id);
                withdrawRequest.Value.Success = false;
                withdrawRequest.Value.ErrorDic.TryAdd(extensionKey, CoBoConstant.ResponseNull);
            }
            else
            {
                _logger.LogInformation("withdraw order {Id} stream, request to withdraw success.",
                    orderDto.Id);

                withdrawRequest.Value.Success = true;
            }
        }
        catch (Exception e)
        {
            HandleWithdrawRequestException(extensionKey, withdrawRequest, e);
        }
    }

    private async Task HandleFailRequest(WithdrawOrderDto order,
        KeyValuePair<Guid, WithdrawRequestInfo> withdrawRequest)
    {
        if (withdrawRequest.Value.RetryCount < RETRYCOUNT)
        {
            return;
        }

        order.Status = OrderStatusEnum.ToTransferFailed.ToString();
        order.ExtensionInfo ??= new Dictionary<string, string>();
        foreach (var errorInfo in withdrawRequest.Value.ErrorDic)
        {
            order.ExtensionInfo.TryAdd(errorInfo.Key, errorInfo.Value);
        }

        await UpdateOrderStatus(order);
    }

    private async Task UpdateOrderStatus(WithdrawOrderDto order)
    {
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(order.Id);
        await userWithdrawGrain.AddOrUpdateOrder(order);
    }

    private string GetWithdrawErrorKey(int retryCount = 0) =>
        $"{CommonConstant.WithdrawRequestErrorKey}_{retryCount}";
    
    private string KeyMapping(string key)
    {
        return _coBoOptions.Value.KeyMapping.GetValueOrDefault(key, key);
    }

    private void HandleWithdrawRequestException(string extensionKey,
        KeyValuePair<Guid, WithdrawRequestInfo> withdrawRequest,
        Exception exception)
    {
        if (exception is UserFriendlyException { Code: CommonConstant.ThirdPartResponseCode.DuplicateRequest })
        {
            _logger.LogInformation("duplicate withdraw requestId:{requestId}", withdrawRequest.Key);
            withdrawRequest.Value.Success = true;
            return;
        }

        _logger.LogError(exception, "withdraw order {Id} stream error, request to withdraw error.",
            withdrawRequest.Key);
        withdrawRequest.Value.Success = false;
        withdrawRequest.Value.ErrorDic.TryAdd(extensionKey, exception.Message);
    }

    private void HandleException()
    {
        if (State.WithdrawRequestMap == null)
        {
            _logger.LogError("State.WithdrawRequestMap is null");
            State.WithdrawRequestMap ??= new Dictionary<Guid, WithdrawRequestInfo>();
        }

        if (State.WithdrawInfoMap == null)
        {
            _logger.LogError("State.WithdrawInfoMap is null");
            State.WithdrawInfoMap ??= new Dictionary<Guid, WithdrawInfo>();
        }
    }
}