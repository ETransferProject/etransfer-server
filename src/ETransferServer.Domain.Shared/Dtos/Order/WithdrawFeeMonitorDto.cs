using System;
using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class WithdrawFeeMonitorDto
{

    public Guid OrderId { get; set; }
    public DateTime FeeTime { get; set; }
    public List<FeeInfo> FeeInfos { get; set; } = new();

}