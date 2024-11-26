using System;
using ETransferServer.Common;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class TransferOrderMonitorDto
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string OrderType { get; set; }
    [Id(2)] public string OrderId { get; set; }
    [Id(3)] public string FromNetwork { get; set; }
    [Id(4)] public string ToNetwork { get; set; }
    [Id(5)] public string Symbol { get; set; }
    [Id(6)] public string Amount { get; set; }
    [Id(7)] public string Reason { get; set; }
    
    public static TransferOrderMonitorDto Create(OrderIndexDto orderDto, string id, string reason)
    {
        return new TransferOrderMonitorDto
        {
            Id = id,
            OrderType = OrderTypeEnum.Transfer.ToString(),
            OrderId = orderDto.Id == Guid.Empty ? null : orderDto.Id.ToString(),
            FromNetwork = orderDto.FromTransfer?.Network,
            ToNetwork = orderDto.ToTransfer?.Network,
            Symbol = orderDto.FromTransfer?.Symbol,
            Amount = orderDto.FromTransfer?.Amount,
            Reason = reason
        };
    }
}