using ETransferServer.Common;
using ETransferServer.Grains.State.Order;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace ETransferServer.Grains.Grain.Order;

public interface IOrderTxFlowGrain : IGrainWithGuidKey
{
    public Task<bool> AddOrUpdate(OrderTxData txData);
    public Task Reset(string chainId);
    public Task<bool> Check(string chainId);
}

public class OrderTxFlowGrain : Grain<OrderTxFlowState>, IOrderTxFlowGrain
{
    private readonly ILogger<OrderTxFlowGrain> _logger;

    public OrderTxFlowGrain(ILogger<OrderTxFlowGrain> logger)
    {
        _logger = logger;
    }
    
    public async Task<bool> AddOrUpdate(OrderTxData txData)
    {
        _logger.LogInformation("OrderTxFlow save: {OrderId}, {TxId}, {ChainId}, {Status}",
            this.GetPrimaryKey(), txData.TxId, txData.ChainId, txData.Status);
        State.Id = this.GetPrimaryKey();
        State.OrderTxInfo.AddOrReplace(txData.TxId, txData);
        await WriteStateAsync();

        return true;
    }
    
    public async Task Reset(string chainId)
    {
        if (State.Id == null || State.Id == Guid.Empty)
        {
            return;
        }

        foreach (var item in State.OrderTxInfo)
        {
            if (item.Value.ChainId == chainId
                && item.Value.Status == ThirdPartOrderStatusEnum.Pending.ToString())
            {
                _logger.LogInformation("OrderTxFlow reset: {OrderId}, {TxId}, {ChainId}, {Status}",
                    State.Id, item.Key, item.Value.ChainId, item.Value.Status);
                item.Value.Status = ThirdPartOrderStatusEnum.Fail.ToString();
            }
        }
        await WriteStateAsync();
    }

    public async Task<bool> Check(string chainId)
    {
        if (State.Id == null || State.Id == Guid.Empty)
        {
            return true;
        }

        foreach (var item in State.OrderTxInfo)
        {
            _logger.LogInformation("OrderTxFlow check: {OrderId}, {TxId}, {ChainId}, {Status}",
                State.Id, item.Key, item.Value.ChainId, item.Value.Status);
            if (item.Value.ChainId == chainId
                && (item.Value.Status == ThirdPartOrderStatusEnum.Pending.ToString()
                    || item.Value.Status == ThirdPartOrderStatusEnum.Success.ToString()))
            {
                return false;
            }
        }
        
        return true;
    }
}