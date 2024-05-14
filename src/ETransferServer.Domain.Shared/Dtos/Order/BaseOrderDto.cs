using System;
using System.Collections.Generic;
using ETransferServer.Common;

namespace ETransferServer.Dtos.Order;

public class BaseOrderDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string OrderType { get; set; }
    public string ThirdPartOrderId { get; set; }
    /// <see cref="ThirdPartServiceNameEnum"/>
    public string ThirdPartServiceName { get; set; }
    public TransferInfo FromTransfer { get; set; }
    public TransferInfo ToTransfer { get; set; }
    public long? LastModifyTime { get; set; }
    public long? CreateTime { get; set; }
    public long? ExpireTime { get; set; }
    public long? ArrivalTime { get; set; }
    public string Status { get; set; }
    public Dictionary<string, string> ExtensionInfo { get; set; }
}

public class TransferInfo
{
    public string Network { get; set; }
    public string ChainId { get; set; }
    public string TxId { get; set; }
    public long? TxTime { get; set; }
    public long? TxHeight { get; set; }
    public string Symbol { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
    public string FromAddress { get; set; }
    public string ToAddress { get; set; }
    public string BlockHash { get; set; }
    public List<FeeInfo> FeeInfo { get; set; }
}