using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class DepositSwapMonitorDto
{
    [Id(0)] public string OrderId { get; set; }
    [Id(1)] public string OrderType { get; set; }
    [Id(2)] public string UserId { get; set; }
    [Id(3)] public string txId { get; set; }
    [Id(4)] public string FromSymbol { get; set; }
    [Id(5)] public string ToSymbol { get; set; }
    [Id(6)] public string NetWork { get; set; }
    [Id(7)] public string ToChainId { get; set; }
    [Id(8)] public decimal FromAmount { get; set; }
    [Id(9)] public string Reason { get; set; }
    [Id(10)] public long? CreateTime { get; set; }
    
    public static DepositSwapMonitorDto Create(DepositOrderDto orderDto, string reason)
    {
        var depositSwapMonitorDto = new DepositSwapMonitorDto();
        depositSwapMonitorDto.CopyPropertiesFromOrder(orderDto);
        depositSwapMonitorDto.Reason = reason;
        return depositSwapMonitorDto;
    }

    private void CopyPropertiesFromOrder(DepositOrderDto orderDto)
    {
        OrderId = orderDto.Id.ToString();
        OrderType = orderDto.OrderType;
        UserId = orderDto.UserId.ToString();
        txId = orderDto.ToTransfer.TxId;
        FromSymbol = orderDto.FromTransfer.Symbol;
        ToSymbol = orderDto.ToTransfer.Symbol;
        NetWork = orderDto.FromTransfer.Network;
        ToChainId = orderDto.ToTransfer.ChainId;
        FromAmount = orderDto.FromTransfer.Amount;
        CreateTime = orderDto.CreateTime;
    }
}