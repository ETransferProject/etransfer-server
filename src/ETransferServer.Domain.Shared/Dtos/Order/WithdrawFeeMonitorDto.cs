using System;
using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class WithdrawFeeMonitorDto
{

    [Id(0)] public Guid OrderId { get; set; }
    [Id(1)] public DateTime FeeTime { get; set; }
    [Id(2)] public List<FeeInfo> FeeInfos { get; set; } = new();

}