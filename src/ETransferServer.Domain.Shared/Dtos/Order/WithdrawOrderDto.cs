using System.Collections.Generic;

namespace ETransferServer.Dtos.Order;

public class WithdrawOrderDto : BaseOrderDto
{
    public string RawTransaction { get; set; }
    public decimal AmountUsd { get; set; }
    public List<FeeInfo> ThirdPartFee { get; set; } = new();
    public string? FromRawTransaction { get; set; }
}

public class WithdrawOrderChangeDto
{
    public WithdrawOrderDto WithdrawOrder { get; set; }
    public Dictionary<string, string> ExtensionData { get; set; }
}