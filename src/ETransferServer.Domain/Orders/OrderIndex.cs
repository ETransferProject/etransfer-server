using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Dtos.Order;

namespace ETransferServer.Orders;

public class OrderIndex: OrderBase, IIndexBuild
{
    public string RawTransaction { get; set; }
    public List<FeeInfo> ThirdPartFee { get; set; } = new();
}