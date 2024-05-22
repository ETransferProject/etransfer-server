namespace ETransferServer.Dtos.Order;

public class DepositSwapMonitorDto
{
    public string OrderId { get; set; }
    public string OrderType { get; set; }
    public string UserId { get; set; }
    public string txId { get; set; }
    public string FromSymbol { get; set; }
    public string ToSymbol { get; set; }
    public string NetWork { get; set; }
    public string ToChainId { get; set; }
    public decimal FromAmount { get; set; }
    public string Reason { get; set; }
    public long? CreateTime { get; set; }
    
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