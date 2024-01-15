using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class WithdrawOrderDto : BaseOrderDto
{
    public string RawTransaction { get; set; }
    public List<FeeInfo> ThirdPartFee { get; set; } = new();
}