using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.TokenLimit;
using ETransferServer.Grains.State.Order;

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

        var status = Enum.Parse<OrderStatusEnum>(orderDto.Status);
        switch (status)
        {
            case OrderStatusEnum.Initialized:
            case OrderStatusEnum.Created:
            case OrderStatusEnum.Pending:
                orderDto.Status = OrderStatusEnum.FromStartTransfer.ToString();
                await AddOrUpdateOrder(orderDto);
                break;
            case OrderStatusEnum.FromStartTransfer:
                await TransferForward(orderDto);
                break;

            // Transactions that have been successfully sent but have not been confirmed
            // are registered to the timer query.
            case OrderStatusEnum.FromTransferring:
            case OrderStatusEnum.FromTransferred:
                await _withdrawTimerGrain.AddToPendingList(orderDto.Id, new TimerTransaction
                {
                    TxId = orderDto.FromTransfer.TxId,
                    TxTime = orderDto.FromTransfer.TxTime,
                    ChainId = orderDto.FromTransfer.ChainId,
                    TransferType = TransferTypeEnum.FromTransfer.ToString()
                });
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
                await _withdrawQueryTimerGrain.AddToRequest(orderDto);
                break;
            case OrderStatusEnum.ToTransferring:
                _logger.LogInformation("withdraw before add to query, orderId:{orderId}", orderDto.Id);
                await _withdrawQueryTimerGrain.AddToQuery(orderDto);
                break;
            case OrderStatusEnum.ToTransferred:
                break;
            case OrderStatusEnum.ToTransferConfirmed:
                orderDto.Status = OrderStatusEnum.Finish.ToString();
                await AddOrUpdateOrder(orderDto);
                break;
            case OrderStatusEnum.ToTransferFailed:
                _logger.LogError("Order {Id} ToTransferFailed, invalid status, current status={Status}",
                    this.GetPrimaryKey(),
                    status.ToString());
                await ReverseTokenLimitAsync(orderDto.Id, orderDto.ToTransfer.Symbol, orderDto.AmountUsd);
                break;
            
            // To completed stream
            case OrderStatusEnum.Finish:
                _logger.LogInformation("Order {Id} stream end, current status={Status}", this.GetPrimaryKey(),
                    status.ToString());
                break;
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Failed:
                _logger.LogInformation("Order {Id} stream end, current status={Status}", this.GetPrimaryKey(),
                    status.ToString());
                await ReverseTokenLimitAsync(orderDto.Id, orderDto.ToTransfer.Symbol, orderDto.AmountUsd);
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

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "withdraw order {Id} stream OnError", this.GetPrimaryKey());
        return Task.CompletedTask;
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
        _logger.LogInformation("set token limit, orderId:{orderId}, grainId:{grainId}, amount:{amount}", orderId,
            limitGrainId, -amount);
    }
    
    private async Task<long> GetOrderCreateTime(Guid orderId)
    {
        var recordGrain = GrainFactory.GetGrain<IUserWithdrawRecordGrain>(orderId);
        var res = await recordGrain.GetAsync();
        if (!res.Success)
        {
            _logger.LogWarning("Withdraw order {OrderId} not found when revert token limit", orderId);
            return 0;
        }
        return (res.Data as WithdrawOrderDto)?.CreateTime ?? 0;
    }
}