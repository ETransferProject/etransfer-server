using System;
using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class OrderIndexDto
{
    public Guid Id { get; set; }
    public string OrderType { get; set; }
    public string Status { get; set; }
    public long LastModifyTime { get; set; }
    public long ArrivalTime { get; set; }
    public TransferInfoDto FromTransfer { get; set; }
    public TransferInfoDto ToTransfer { get; set; }
}

public class TransferInfoDto
{
    public string Network { get; set; }
    public string ChainId { get; set; }
    public string Symbol { get; set; }
    public string Amount { get; set; }
    public string FromAddress { get; set; }
    public string ToAddress { get; set; }
    public List<FeeInfo> FeeInfo { get; set; } = new();
}

public class OrderStatusDto
{
    public bool Status { get; set; }
}

public class OrderReadDto
{
    
}