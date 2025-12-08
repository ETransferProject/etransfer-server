using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Dtos.Order;

namespace ETransferServer.Orders;

public class OrderStatusFlow :  OrderEntity<Guid>, IIndexBuild
{
    public List<OrderStatus> StatusFlow { get; set; } = new();
}