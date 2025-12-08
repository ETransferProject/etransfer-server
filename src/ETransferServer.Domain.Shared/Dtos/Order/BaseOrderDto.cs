using System;
using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class BaseOrderDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public string OrderType { get; set; }
    [Id(3)] public string ThirdPartOrderId { get; set; }
    /// <see cref="ThirdPartServiceNameEnum"/>
    [Id(4)] public string ThirdPartServiceName { get; set; }
    [Id(5)] public TransferInfo FromTransfer { get; set; }
    [Id(6)] public TransferInfo ToTransfer { get; set; }
    [Id(7)] public long? LastModifyTime { get; set; }
    [Id(8)] public long? CreateTime { get; set; }
    [Id(9)] public long? ExpireTime { get; set; }
    [Id(10)] public long? ArrivalTime { get; set; }
    [Id(11)] public string Status { get; set; }
    [Id(12)] public Dictionary<string, string> ExtensionInfo { get; set; }
}

[GenerateSerializer]
public class TransferInfo
{
    [Id(0)] public string Network { get; set; }
    [Id(1)] public string ChainId { get; set; }
    [Id(2)] public string TxId { get; set; }
    [Id(3)] public long? TxTime { get; set; }
    [Id(4)] public long? TxHeight { get; set; }
    [Id(5)] public string Symbol { get; set; }
    [Id(6)] public decimal Amount { get; set; }
    [Id(7)] public string Status { get; set; }
    [Id(8)] public string FromAddress { get; set; }
    [Id(9)] public string ToAddress { get; set; }
    [Id(10)] public string BlockHash { get; set; }
    [Id(11)] public List<FeeInfo> FeeInfo { get; set; }
}