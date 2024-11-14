using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Timers;

public interface IWithdrawCoboTimerGrain : IGrainWithGuidKey
{
    public Task AddToRequest(WithdrawOrderDto order);
    public Task AddToMap(WithdrawOrderDto order, string message);
    public Task<DateTime> GetLastCallBackTime();
}

public class WithdrawCoboTimerGrain : Grain<WithdrawCoboTimerState>, IWithdrawCoboTimerGrain
{
    private readonly ILogger<WithdrawTimerGrain> _logger;
    private readonly TimerOptions _timerOptions;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;

    private const int RETRYCOUNT = 4;

    public WithdrawCoboTimerGrain(ILogger<WithdrawTimerGrain> logger, 
        IOptionsSnapshot<TimerOptions> timerOptions,
        ICoBoProvider coBoProvider, 
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions)
    {
        _logger = logger;
        _coBoProvider = coBoProvider;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _timerOptions = timerOptions.Value;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("WithdrawCoboTimerGrain {Id} Activate", this.GetPrimaryKey().ToString());

        await base.OnActivateAsync(cancellationToken);
        await StartWithRequestTimer(TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.PeriodSeconds),
            TimeSpan.FromSeconds(_timerOptions.WithdrawTimer.DelaySeconds));

        State.WithdrawRequestMap ??= new Dictionary<Guid, WithdrawRequestInfo>();
    }

    private Task StartWithRequestTimer(TimeSpan timerPeriod, TimeSpan delayPeriod)
    {
        _logger.LogDebug("WithdrawCoboTimerGrain StartTimer {StartTime}", DateTime.UtcNow.ToUtc8String());
        RegisterTimer(RequestCallback, delayPeriod, TimeSpan.Zero, timerPeriod);
        return Task.CompletedTask;
    }

    private async Task RequestCallback(object state)
    {
        try
        {
            var total = State.WithdrawRequestMap.Count;
            _logger.LogDebug("WithdrawCoboTimerGrain request callback, Count={Count}", total);
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
            _logger.LogInformation("WithdrawCoboTimerGrain finish, count: {Removed}/{Total}", removed, total);

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
        var callGrain = GrainFactory.GetGrain<IWithdrawOrderCallGrain>(order.Id);
        try
        {
            var orderGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(order.Id);
            var orderResult = await orderGrain.Get();
            if (orderResult?.Data == null)
            {
                _logger.LogWarning("add to request fail, order not exists, orderId:{orderId}", order.Id);
                return;
            }

            await callGrain.AddOrGet(2);
            var result = await Withdraw(order);

            await callGrain.AddOrGet(3);
            if (result.success)
            {
                order.Status = OrderStatusEnum.ToTransferring.ToString();
                await UpdateOrderStatus(order);
                return;
            }

            await AddToMap(order, result.message);
        }
        catch (Exception e)
        {
            await callGrain.AddOrGet(4);
            _logger.LogError(e, "add to request error, orderId:{orderId}", order.Id);
        }
    }

    public async Task AddToMap(WithdrawOrderDto order, string message)
    {
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
                [errorKey] = message
            }
        };
        await WriteStateAsync();
    }

    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(DateTime.Now);
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
    }
}