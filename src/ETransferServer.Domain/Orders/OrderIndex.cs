using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;

namespace ETransferServer.Orders;

public class OrderIndex: OrderBase, IIndexBuild
{
    [Keyword] public string RawTransaction { get; set; }
    public List<Fee> ThirdPartFee { get; set; } = new();
}