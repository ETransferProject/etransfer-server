using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using ETransferServer.ThirdPart.CoBo;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IWithdrawOrderCallGrain : IGrainWithGuidKey
{
    Task<int> AddOrGet(int status = 0);
    Task<bool> AddRetry();
    Task AddToRequest(WithdrawOrderDto order);
    Task AddToQuery(WithdrawOrderDto order);
}

public class WithdrawOrderCallGrain : Grain<WithdrawOrderCallState>, IWithdrawOrderCallGrain
{
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    private readonly ILogger<WithdrawOrderCallGrain> _logger;

    public WithdrawOrderCallGrain(ICoBoProvider coBoProvider, 
        IOptionsSnapshot<WithdrawOptions> withdrawOptions,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<CoBoOptions> coBoOptions,
        ILogger<WithdrawOrderCallGrain> logger)
    {
        _coBoProvider = coBoProvider;
        _withdrawOptions = withdrawOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _coBoOptions = coBoOptions;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<int> AddOrGet(int status = 0)
    {
        _logger.LogInformation("WithdrawOrderCallGrain, orderId:{orderId}, status:{status}", 
            this.GetPrimaryKey(), status);
        if (status == 0) return State.Status;
        State.OrderId = this.GetPrimaryKey();
        State.Status = status;
        await WriteStateAsync();
        return status;
    }

    public async Task<bool> AddRetry()
    {
        State.OrderId = this.GetPrimaryKey();
        if (State.Status >= 1 && State.Status <= 2)
        {
            State.CallRetry += 1;
            _logger.LogInformation(
                "WithdrawOrderCallGrain, orderId:{orderId}, callRetry:{callRetry}, callMaxRetry:{callMaxRetry}",
                this.GetPrimaryKey(), State.CallRetry, _withdrawOptions.Value.CallMaxRetry);
            if (State.CallRetry > _withdrawOptions.Value.CallMaxRetry)
                return false;
        }
        else if(State.Status >= 3 && State.Status <= 4)
        {
            State.CallbackRetry += 1;
            _logger.LogInformation(
                "WithdrawOrderCallGrain, orderId:{orderId}, callbackRetry:{callbackRetry}, callbackMaxRetry:{callbackMaxRetry}",
                this.GetPrimaryKey(), State.CallbackRetry, _withdrawOptions.Value.CallbackMaxRetry);
            if (State.CallbackRetry > _withdrawOptions.Value.CallbackMaxRetry)
                return false;
        }
        else if (State.Status > 4)
        {
            State.CallQueryRetry += 1;
            _logger.LogInformation(
                "WithdrawOrderCallGrain, orderId:{orderId}, callQueryRetry:{callQueryRetry}, callQueryMaxRetry:{callQueryMaxRetry}",
                this.GetPrimaryKey(), State.CallQueryRetry, _withdrawOptions.Value.CallQueryMaxRetry);
            if (State.CallQueryRetry > _withdrawOptions.Value.CallQueryMaxRetry)
                return false;
        }

        await WriteStateAsync();
        return true;
    }

    public async Task AddToRequest(WithdrawOrderDto order)
    {
        _logger.LogInformation("WithdrawOrderCallGrain addToRequest, orderId:{orderId}", this.GetPrimaryKey());
        if (State.CallRetry > _withdrawOptions.Value.CallMaxRetry)
        {
            _logger.LogError("WithdrawOrderCallGrain addToRequest after retry {times}, {orderId}",
                State.CallRetry, this.GetPrimaryKey());
            await HandleWithdrawAsync(order);
            return;
        }
        try
        {
            var _withdrawCoboTimerGrain =
                GrainFactory.GetGrain<IWithdrawCoboTimerGrain>(GuidHelper.UniqGuid(nameof(IWithdrawCoboTimerGrain)));
            await _withdrawCoboTimerGrain.AddToRequest(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WithdrawOrderCallGrain addToRequest error, {times},{id}", 
                State.CallRetry, this.GetPrimaryKey());
            State.CallRetry += 1;
            await WriteStateAsync();
            await Task.Delay(3000);
            await AddToRequest(order);
        }
    }
    
    public async Task AddToQuery(WithdrawOrderDto order)
    {
        _logger.LogInformation("WithdrawOrderCallGrain addToQuery, orderId:{orderId}", this.GetPrimaryKey());
        if (State.CallQueryRetry > _withdrawOptions.Value.CallQueryMaxRetry)
        {
            _logger.LogError("WithdrawOrderCallGrain addToQuery after retry {times}, {orderId}",
                State.CallQueryRetry, this.GetPrimaryKey());
            return;
        }
        try
        {
            var _withdrawTimerGrain =
                GrainFactory.GetGrain<IWithdrawTimerGrain>(GuidHelper.UniqGuid(nameof(IWithdrawTimerGrain)));
            await _withdrawTimerGrain.AddToQuery(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WithdrawOrderCallGrain addToQuery error, {times},{id}", 
                State.CallQueryRetry, this.GetPrimaryKey());
            State.CallQueryRetry += 1;
            await WriteStateAsync();
            await Task.Delay(3000);
            await AddToQuery(order);
        }
    }
    
    private async Task HandleWithdrawAsync(WithdrawOrderDto order)
    {
        try
        {
            await AddOrGet(2);
            var result = await Withdraw(order);

            await AddOrGet(3);
            if (result.success)
            {
                order.Status = OrderStatusEnum.ToTransferring.ToString();
                await UpdateOrderStatus(order);
                return;
            }

            var _withdrawCoboTimerGrain =
                GrainFactory.GetGrain<IWithdrawCoboTimerGrain>(GuidHelper.UniqGuid(nameof(IWithdrawCoboTimerGrain)));
            await _withdrawCoboTimerGrain.AddToMap(order, result.message);
        }
        catch (Exception e)
        {
            await AddOrGet(4);
            _logger.LogError(e, "WithdrawOrderCallGrain add to request error, orderId:{orderId}", order.Id);
        }
    }
    
    private async Task UpdateOrderStatus(WithdrawOrderDto order)
    {
        var userWithdrawGrain = GrainFactory.GetGrain<IUserWithdrawGrain>(order.Id);
        await userWithdrawGrain.AddOrUpdateOrder(order);
    }

    private async Task<(bool success, string message)> Withdraw(WithdrawOrderDto orderDto)
    {
        try
        {
            var coinInfo =
                _withdrawNetworkOptions.Value.GetNetworkInfo(orderDto.ToTransfer.Network, orderDto.ToTransfer.Symbol);
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
                _logger.LogWarning("WithdrawOrderCallGrain withdraw order {Id} stream error, request to withdraw fail.",
                    orderDto.Id);
                return (false, CoBoConstant.ResponseNull);
            }
            else
            {
                _logger.LogInformation(
                    "WithdrawOrderCallGrain withdraw order {Id} stream, request to withdraw success.",
                    orderDto.Id);
                return (true, string.Empty);
            }
        }
        catch (Exception e)
        {
            if (e is UserFriendlyException { Code: CommonConstant.ThirdPartResponseCode.DuplicateRequest })
            {
                _logger.LogInformation("WithdrawOrderCallGrain duplicate withdraw requestId:{requestId}", orderDto.Id);
                return (true, string.Empty);
            }

            try
            {
                _logger.LogError(e,
                    "WithdrawOrderCallGrain withdraw order {Id} stream error, request to withdraw error.", orderDto.Id);
                return (false, e.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(CoBoConstant.ResponseParseError);
                return (false, CoBoConstant.ResponseParseError);
            }
        }
    }
}