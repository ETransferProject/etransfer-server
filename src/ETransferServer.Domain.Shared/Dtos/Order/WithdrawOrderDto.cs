using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class WithdrawOrderDto : BaseOrderDto
{
    [Id(0)] public string RawTransaction { get; set; }
    [Id(1)] public decimal AmountUsd { get; set; }
    [Id(2)] public List<FeeInfo> ThirdPartFee { get; set; } = new();
    [Id(3)] public string? FromRawTransaction { get; set; }
}

[GenerateSerializer]
public class WithdrawOrderChangeDto
{
    [Id(0)] public WithdrawOrderDto WithdrawOrder { get; set; }
    [Id(1)] public Dictionary<string, string> ExtensionData { get; set; }
}