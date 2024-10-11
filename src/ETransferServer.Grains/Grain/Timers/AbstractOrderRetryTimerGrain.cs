using Microsoft.Extensions.Logging;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;

namespace ETransferServer.Grains.Grain.Timers;

public abstract class AbstractOrderRetryTimerGrain<TOrder> : Grain<OrderRetryState> where TOrder : BaseOrderDto
{
    internal DateTime LastCallBackTime;

    public readonly ILogger<AbstractOrderRetryTimerGrain<TOrder>> Logger;

    protected AbstractOrderRetryTimerGrain(ILogger<AbstractOrderRetryTimerGrain<TOrder>> logger)
    {
        Logger = logger;
    }

    /// <summary>
    ///     You need to implement the class to implement the save logic.
    ///     After the save is successful,
    ///     you may need to push the order back to Stream for processing.
    /// </summary>
    /// <param name="order"></param>
    /// <param name="externalInfo"></param>
    /// <returns></returns>
    protected abstract Task SaveOrder(TOrder order, Dictionary<string, string> externalInfo);

    /// <summary>
    ///     You need to implement the class to implement the logic of querying orders.
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    protected abstract Task<TOrder> GetOrder(Guid orderId);

    /// <summary>
    ///     Query the last callback time, which can be used to activate Grain
    /// </summary>
    /// <returns></returns>
    public Task<DateTime> GetLastCallBackTime()
    {
        return Task.FromResult(LastCallBackTime);
    }

    /// <summary>
    ///     Register orders that need to be retried to Timer
    /// </summary>
    /// <param name="orderId">Orders to retry</param>
    /// <param name="retryFromStatus">Retry from this state</param>
    public async Task AddToPendingList(Guid orderId, string retryFromStatus)
    {
        if (State.OrderRetryData.ContainsKey(orderId))
        {
            Logger.LogWarning("Order {OrderId} already in retry list", orderId);
            return;
        }

        var state = Enum.TryParse<OrderStatusEnum>(retryFromStatus, out _);
        AssertHelper.IsTrue(state, "Invalid order {OrderId} retry from status {Status}", orderId, retryFromStatus);

        State.OrderRetryData[orderId] = new OrderRetryFrom
        {
            OrderId = orderId,
            RetryFromState = retryFromStatus,
        };
        
        await WriteStateAsync();
    }

    internal async Task TimerCallBack(object state)
    {
        LastCallBackTime = DateTime.UtcNow;

        var total = State.OrderRetryData.Count;
        Logger.LogDebug("OrderRetryTimerGrain callback, Total={Total}", total);

        if (total < 1) return;
        
        var pendingList = State.OrderRetryData.ToArray();
        foreach (var (orderId, status) in pendingList)
        {
            // remove invalid data in state
            if (orderId == Guid.Empty || status == null || status.OrderId == Guid.Empty || status.RetryFromState.IsNullOrEmpty())
            {
                Logger.LogError("Pending retry order {OrderId} data invalid", orderId);
                State.OrderRetryData.Remove(orderId, out _);
                continue;
            }
            
            var order = await GetOrder(orderId);
            if (order == null)
            {
                // order not found, This shouldn't happen
                Logger.LogError("Pending retry order {OrderId} not found", orderId);
                State.OrderRetryData.Remove(orderId, out _);
                continue;
            }

            // update order to retry state
            order.Status = status.RetryFromState;
            await SaveOrder(order, ExtensionBuilder.New().Build());
            State.OrderRetryData.Remove(orderId);
            await WriteStateAsync();
        }

    }  
}