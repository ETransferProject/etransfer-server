using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;
using Newtonsoft.Json;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public partial class UserDepositGrain
{
    
    /// <summary>
    ///     Receive flow data and process subsequent processes based on order status
    /// </summary>
    /// <param name="orderDto"></param>
    /// <param name="token"></param>
    public async Task OnNextAsync(DepositOrderDto orderDto, StreamSequenceToken token = null)
    {
        
        // Bottom logic to prevent Stream dead loops
        if (++ _currentSteps >= MaxStreamSteps)
        {
            _logger.LogError("Too many stream steps, orderId={OrderId}", orderDto.Id);
            return;
        }
        
        _logger.LogInformation("Deposit order in stream, orderId:{orderId}, status:{status}", orderDto.Id,
            orderDto.Status);
        var status = Enum.Parse<OrderStatusEnum>(orderDto.Status);
        switch (status)
        {
            // The confirmed state is when the charge order is obtained from a third party,
            // which is the first state of the charge order.
            case OrderStatusEnum.FromTransferConfirmed:
            {
                await AddCheckOrder(orderDto);
                orderDto.Status = OrderStatusEnum.ToStartTransfer.ToString();
                await AddOrUpdateOrder(orderDto);
                break;
            }

            // Send transaction to node 
            case OrderStatusEnum.ToStartTransfer:
            {
                var order = await OnToStartTransfer(orderDto);
                await AddOrUpdateOrder(order.DepositOrder, order.ExtensionData);
                break;
            }

            // ToTransferring,ToTransferred states need to wait for the transaction result and multiple confirmations,
            // and register them in Timer for processing. After Timer waits for the result,
            // the transaction will return to the stream.
            case OrderStatusEnum.ToTransferring:
            case OrderStatusEnum.ToTransferred:
            {
                await OnToTransferred(orderDto);
                break;
            }

            // Finish
            case OrderStatusEnum.ToTransferConfirmed:
                orderDto.Status = OrderStatusEnum.Finish.ToString();
                await AddOrUpdateOrder(orderDto);
                break;

            // Retry with max count
            case OrderStatusEnum.ToTransferFailed:
            {
                var statusFlow = await _orderStatusFlowGrain.GetAsync();
                var querySuccess = statusFlow?.Data != null;
                var retryFrom = OrderStatusEnum.ToStartTransfer.ToString();
                var maxRetry = _depositOptions.Value.ToTransferMaxRetry;
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
                    await _depositOrderRetryTimerGrain.AddToPendingList(orderDto.Id, retryFrom);
                }
                break;
            }

            // To completed stream
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Finish:
            case OrderStatusEnum.Failed:
                _logger.LogInformation("Order {Id} stream end, current status={Status}", this.GetPrimaryKey(),
                    status.ToString());
                await HandleDepositQueryGrain(orderDto.ThirdPartOrderId);
                // await _orderChangeStream.OnCompletedAsync();
                break;


            // Deposit business does not involve these states
            case OrderStatusEnum.Initialized:
            case OrderStatusEnum.Created:
            case OrderStatusEnum.Pending:
            case OrderStatusEnum.FromStartTransfer:
            case OrderStatusEnum.FromTransferring:
            case OrderStatusEnum.FromTransferred:
            default:
                _logger.LogError("Order {Id} stream error, invalid status, current status={Status}",
                    this.GetPrimaryKey(),
                    status.ToString());
                break;
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Order {Id} stream Completed", this.GetPrimaryKey());
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Order {Id} stream OnError", this.GetPrimaryKey());
        return Task.CompletedTask;
    }

    private async Task HandleDepositQueryGrain(string transactionId)
    {
        await _depositQueryTimerGrain.Remove(transactionId);
    }

}