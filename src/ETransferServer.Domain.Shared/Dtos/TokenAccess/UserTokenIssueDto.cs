using System;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class UserTokenIssueDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Address { get; set; }
    [Id(2)] public string WalletAddress { get; set; }
    [Id(3)] public string Symbol { get; set; }
    [Id(4)] public string ChainId { get; set; }
    [Id(5)] public long CreateTime { get; set; }
    [Id(6)] public long UpdateTime { get; set; }
    [Id(7)] public string TokenName { get; set; }
    [Id(8)] public string TokenImage { get; set; }
    [Id(9)] public string OtherChainId { get; set; }
    [Id(10)] public string TotalSupply { get; set; }
    [Id(11)] public string ContractAddress { get; set; }
    [Id(12)] public string BindingId { get; set; }
    [Id(13)] public string ThirdTokenId { get; set; }
    [Id(14)] public string Status { get; set; }
}