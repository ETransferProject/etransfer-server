using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.Token;

[GenerateSerializer]
public class TokenPoolDto
{
    [Id(0)] public string Date { get; set; }
    [Id(1)] public long LastModifyTime { get; set; }
    [Id(2)] public Dictionary<string, string> MultiPool { get; set; } = new();
    [Id(3)] public Dictionary<string, string> TokenPool { get; set; } = new();
    [Id(4)] public Dictionary<string, string> ThirdFeeInfo { get; set; } = new();
    [Id(5)] public Dictionary<string, string> ThirdPoolFeeInfo { get; set; } = new();
    [Id(6)] public Dictionary<string, string> Pool { get; set; } = new();
    [Id(7)] public Dictionary<string, string> WithdrawFeeInfo { get; set; } = new();
    [Id(8)] public Dictionary<string, string> DepositFeeInfo { get; set; } = new();
}