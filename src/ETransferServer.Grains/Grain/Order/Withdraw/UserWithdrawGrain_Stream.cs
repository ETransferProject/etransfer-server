using Microsoft.Extensions.Logging;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.State.Order;
using NBitcoin;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public partial class UserWithdrawGrain
{
    public async Task OnNextAsync(WithdrawOrderDto orderDto, StreamSequenceToken token = null)
    {
        if (++_currentSteps >= MaxStreamSteps)
        {
            _logger.LogError("Too many stream steps, orderId={OrderId}", orderDto.Id);
            return;
        }

        _logger.LogInformation("withdraw order in stream, orderId:{orderId}, status:{status}", orderDto.Id,
            orderDto.Status);

        var isAElf = orderDto.ToTransfer.Network == CommonConstant.Network.AElf;
        var status = Enum.Parse<OrderStatusEnum>(orderDto.Status);
        switch (status)
        {
            case OrderStatusEnum.Initialized:
            case OrderStatusEnum.Created:
            case OrderStatusEnum.Pending:
                orderDto.Status = OrderStatusEnum.FromStartTransfer.ToString();
                await AddOrUpdateOrder(orderDto);
                await WithdrawLargeAmountAlarmAsync(orderDto);
                await _bus.Publish(_objectMapper.Map<WithdrawOrderDto, OrderChangeEto>(orderDto));
                break;
            case OrderStatusEnum.FromStartTransfer:
                await TransferForward(orderDto);
                break;

            // Transactions that have been successfully sent but have not been confirmed
            // are registered to the timer query.
            case OrderStatusEnum.FromTransferring:
            case OrderStatusEnum.FromTransferred:
                await AddToPendingList(orderDto, TransferTypeEnum.FromTransfer, isAElf);
                break;

            // After the multiple confirmation of the transaction is successful,
            // call the three-party service to transfer the currency to the user's address.
            case OrderStatusEnum.FromTransferConfirmed:
                await AddCheckOrder(orderDto);
                orderDto.Status = OrderStatusEnum.ToStartTransfer.ToString();
                await AddOrUpdateOrder(orderDto);
                break;

            // The transaction fails to end the coin extraction process,
            // and the user can restart the coin extraction.
            case OrderStatusEnum.FromTransferFailed:
                orderDto.Status = OrderStatusEnum.Failed.ToString();
                await AddOrUpdateOrder(orderDto);
                break;

            case OrderStatusEnum.ToStartTransfer:
                _logger.LogInformation("withdraw before add to request, orderId:{orderId}", orderDto.Id);
                await AddToStartTransfer(orderDto, isAElf);
                break;
            case OrderStatusEnum.ToTransferring:
                _logger.LogInformation("withdraw before add to query, orderId:{orderId}", orderDto.Id);
                await AddToTransferring(orderDto, isAElf);
                break;
            case OrderStatusEnum.ToTransferred:
                await AddToPendingList(orderDto, TransferTypeEnum.ToTransfer, isAElf);
                break;
            case OrderStatusEnum.ToTransferConfirmed:
                orderDto.Status = OrderStatusEnum.Finish.ToString();
                await AddOrUpdateOrder(orderDto);
                await SaveOrderTxFlowAsync(orderDto);
                break;
            case OrderStatusEnum.ToTransferFailed:
                _logger.LogError("Order {Id} ToTransferFailed, invalid status, current status={Status}",
                    this.GetPrimaryKey(), status.ToString());
                await SaveOrderTxFlowAsync(orderDto);
                await AddToRetryTx(orderDto);
                break;
            
            // To completed stream
            case OrderStatusEnum.Finish:
                _logger.LogInformation("Order {Id} stream end, current status={Status}", this.GetPrimaryKey(),
                    status.ToString());
                await HandleWithdrawQueryGrain(orderDto.FromTransfer.TxId);
                await ChangeOperationStatus(orderDto);
                await SaveOrderTxFlowAsync(orderDto);
                await _bus.Publish(_objectMapper.Map<WithdrawOrderDto, OrderChangeEto>(orderDto));
                break;
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Failed:
                _logger.LogInformation("Order {Id} stream end, current status={Status}", this.GetPrimaryKey(),
                    status.ToString());
                await ReverseTokenLimitAsync(orderDto.Id, orderDto.ToTransfer.Symbol, orderDto.AmountUsd);
                await HandleWithdrawQueryGrain(orderDto.FromTransfer.TxId);
                await ChangeOperationStatus(orderDto, false);
                await SaveOrderTxFlowAsync(orderDto);
                await _bus.Publish(_objectMapper.Map<WithdrawOrderDto, OrderChangeEto>(orderDto));
                break;

            // Invalid status
            default:
                _logger.LogError("Order {Id} stream error, invalid status, current status={Status}",
                    this.GetPrimaryKey(),
                    status.ToString());
                break;
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("withdraw order {Id} stream Completed", this.GetPrimaryKey());
        return Task.CompletedTask;
    }

    public async Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "withdraw order {Id} stream OnError", this.GetPrimaryKey());
        await HandlerWithdrawErrorAsync();
    }

    private async Task HandlerWithdrawErrorAsync()
    {
        var callGrain = GrainFactory.GetGrain<IWithdrawOrderCallGrain>(this.GetPrimaryKey());
        if (!await callGrain.AddRetry()) return;
        var order = await _recordGrain.Get();
        if (order?.Value == null) return;
        var status = await callGrain.AddOrGet();
        if (status >= 1 && status <= 2)
        {
            await Task.Delay(1000);
            if (_subscriptionHandle != null)
            {
                _logger.LogInformation("Order {Id} stream resumeAsync.", this.GetPrimaryKey());
                _subscriptionHandle.ResumeAsync(this);
            }
            else
            {
                _logger.LogInformation("Order {Id} stream reSubscribeAsync.", this.GetPrimaryKey());
                _subscriptionHandle = await _orderChangeStream.SubscribeAsync(OnNextAsync, OnErrorAsync, OnCompletedAsync);
            }

            _logger.LogInformation("Order {Id} stream addToStartTransfer.", this.GetPrimaryKey());
            await AddToStartTransfer(order.Value, order.Value.ToTransfer.Network == CommonConstant.Network.AElf);
        }
        else if (status >= 3)
        {
            order.Value.Status = OrderStatusEnum.ToTransferring.ToString();
            await AddOrUpdateOrder(order.Value);
        }
    }

    private async Task WithdrawLargeAmountAlarmAsync(WithdrawOrderDto orderDto)
    {
        var withdrawOrderMonitorGrain = GrainFactory.GetGrain<IWithdrawOrderMonitorGrain>(orderDto.Id.ToString());
        await withdrawOrderMonitorGrain.DoLargeAmountMonitor(orderDto);
    }

    private async Task AddToStartTransfer(WithdrawOrderDto orderDto, bool isAElf)
    {
        if (isAElf)
        {
            var order = await OnToStartTransfer(orderDto, true);
            await AddOrUpdateOrder(order.WithdrawOrder, order.ExtensionData);
        }
        else
        {
            var callGrain = GrainFactory.GetGrain<IWithdrawOrderCallGrain>(orderDto.Id);
            await callGrain.AddOrGet(1);
            await _withdrawTimerGrain.AddToRequest(orderDto);
        }
    }
    
    private async Task AddToTransferring(WithdrawOrderDto orderDto, bool isAElf)
    {
        if (isAElf)
        {
            await AddToPendingList(orderDto, TransferTypeEnum.ToTransfer, isAElf);
        }
        else
        {
            await _withdrawTimerGrain.AddToQuery(orderDto);
        }
    }

    private async Task AddToPendingList(WithdrawOrderDto orderDto, TransferTypeEnum TransferType, bool isAElf)
    {
        var transferInfo = TransferType == TransferTypeEnum.ToTransfer ? orderDto.ToTransfer : orderDto.FromTransfer;
        (var id, var tx) = (orderDto.Id, new TimerTransaction
        {
            TxId = transferInfo.TxId,
            TxTime = transferInfo.TxTime,
            ChainId = transferInfo.ChainId,
            TransferType = TransferType.ToString()
        });
        if (isAElf)
        {
            await _withdrawFastTimerGrain.AddToPendingList(id, tx);
        }
        else
        {
            await _withdrawTxTimerGrain.AddToPendingList(id, tx);
        }
    }

    private async Task AddToRetryTx(WithdrawOrderDto orderDto)
    {
        var statusFlow = await _orderStatusFlowGrain.GetAsync();
        var querySuccess = statusFlow?.Data != null;
        var retryFrom = OrderStatusEnum.ToStartTransfer.ToString();
        var maxRetry = _withdrawOptions.Value.ToTransferMaxRetry;
        var maxRetryCountExceeded = querySuccess &&
                                    ((OrderStatusFlowDto)statusFlow.Data).StatusFlow.Count(s =>
                                        s.Status == retryFrom) >= maxRetry;
        if (maxRetryCountExceeded)
        {
            orderDto.Status = OrderStatusEnum.Failed.ToString();
            await AddOrUpdateOrder(orderDto);
        }
        else
        {
            await _withdrawOrderRetryTimerGrain.AddToPendingList(orderDto.Id, retryFrom);
        }
    }

    private async Task ReverseTokenLimitAsync(Guid orderId, string symbol, decimal amount)
    {
        var orderCreateTime = await GetOrderCreateTime(orderId);
        if (orderCreateTime == 0)
        {
            _logger.LogInformation("Withdraw createTime is null, orderId:{OrderId}", orderId);
            return;
        }
        var limitGrainId = ITokenWithdrawLimitGrain.GenerateGrainId(symbol, orderCreateTime);
        var tokenLimitGrain = GrainFactory.GetGrain<ITokenWithdrawLimitGrain>(limitGrainId);
        await tokenLimitGrain.Reverse(amount);
        _logger.LogInformation("Set token limit, orderId:{orderId}, grainId:{grainId}, amount:{amount}", orderId,
            limitGrainId, -amount);
    }
    
    private async Task<long> GetOrderCreateTime(Guid orderId)
    {
        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var res = await recordGrain.Get();
        if (!res.Success)
        {
            _logger.LogWarning("Withdraw order {OrderId} not found when revert token limit", orderId);
            return 0;
        }
        return (res.Data as WithdrawOrderDto)?.CreateTime ?? 0;
    }

    private async Task ChangeOperationStatus(WithdrawOrderDto order, bool success = true)
    {
        order.ExtensionInfo ??= new Dictionary<string, string>();
        if (!order.ExtensionInfo.ContainsKey(ExtensionKey.RelatedOrderId)) return;

        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(Guid.Parse(order.ExtensionInfo[ExtensionKey.RelatedOrderId]));
        var res = await recordGrain.Get();
        if (res.Success)
        {
            var orderRelated = res.Value;
            if (orderRelated.ExtensionInfo.IsNullOrEmpty() || !orderRelated.ExtensionInfo.ContainsKey(ExtensionKey.SubStatus)) return;

            orderRelated.ExtensionInfo.AddOrReplace(ExtensionKey.SubStatus, success
                ? OrderOperationStatusEnum.RefundConfirmed.ToString()
                : OrderOperationStatusEnum.RefundFailed.ToString());
            await recordGrain.AddOrUpdate(orderRelated);
            await _userWithdrawProvider.AddOrUpdateSync(orderRelated);
        }
    }
    
    private async Task SaveOrderTxFlowAsync(WithdrawOrderDto order, string status = null)
    {
        if (order.ToTransfer.TxId.IsNullOrEmpty()) return;
        await _orderTxFlowGrain.AddOrUpdate(new OrderTxData
        {
            TxId = order.ToTransfer.TxId,
            ChainId = order.ToTransfer.ChainId,
            Status = !status.IsNullOrEmpty()
                ? status
                : order.Status == OrderStatusEnum.ToTransferConfirmed.ToString() ||
                  order.Status == OrderStatusEnum.Finish.ToString()
                    ? ThirdPartOrderStatusEnum.Success.ToString()
                    : ThirdPartOrderStatusEnum.Fail.ToString()
        });
    }

    private async Task HandleWithdrawQueryGrain(string transactionId)
    {
        await _withdrawQueryTimerGrain.Remove(transactionId);
    }
}