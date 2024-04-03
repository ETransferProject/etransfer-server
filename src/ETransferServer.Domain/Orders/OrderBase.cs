using System;
using System.Collections.Generic;
using Nest;

namespace ETransferServer.Orders;

public abstract class OrderBase : OrderEntity<Guid>
{
    [Keyword] public Guid UserId { get; set; }
    [Keyword] public string OrderType { get; set; }
    [Keyword] public string ThirdPartOrderId { get; set; }
    [Keyword] public string ThirdPartServiceName { get; set; }
    public Transfer FromTransfer { get; set; }
    public Transfer ToTransfer { get; set; }
    public long LastModifyTime { get; set; }
    public long CreateTime { get; set; }
    public long ExpireTime { get; set; }
    public long ArrivalTime { get; set; }
    [Keyword] public string Status { get; set; }
    public Dictionary<string, string> ExtensionInfo { get; set; }
}

public class Transfer
{
    [Keyword] public string Network { get; set; }
    [Keyword] public string ChainId { get; set; }
    [Keyword] public string TxId { get; set; }
    public long TxTime { get; set; }
    public long TxHeight { get; set; }
    [Keyword] public string Symbol { get; set; }
    public decimal Amount { get; set; }
    [Keyword] public string Status { get; set; }
    [Keyword] public string FromAddress { get; set; }
    [Keyword] public string ToAddress { get; set; }
    public List<Fee> FeeInfo { get; set; }
}

public class Fee
{
    [Keyword] public string Name { get; set; }
    [Keyword] public string Symbol { get; set; }
    [Keyword] public string Amount { get; set; }
    [Keyword] public string Decimals { get; set; }
}