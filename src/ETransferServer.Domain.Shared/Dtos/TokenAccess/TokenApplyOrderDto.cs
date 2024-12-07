using System;
using System.Collections.Generic;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class TokenApplyOrderDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Symbol { get; set; }
    [Id(2)] public string UserAddress { get; set; }
    [Id(3)] public string Status { get; set; }
    [Id(4)] public long CreateTime { get; set; }
    [Id(5)] public long UpdateTime { get; set; }
    [Id(6)] public List<ChainTokenInfoDto> ChainTokenInfo { get; set; }
    [Id(7)] public ChainTokenInfoDto OtherChainTokenInfo { get; set; }
    [Id(8)] public Dictionary<string, string> StatusChangedRecord { get; set; }
}

[GenerateSerializer]
public class ChainTokenInfoDto
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public string ChainName { get; set; }
    [Id(2)] public string TokenName { get; set; }
    [Id(3)] public string Symbol { get; set; }
    [Id(4)] public decimal TotalSupply { get; set; }
    [Id(5)] public int Decimals { get; set; }
    [Id(6)] public string Icon { get; set; }
    [Id(7)] public string PoolAddress { get; set; }
    [Id(8)] public string ContractAddress { get; set; }
    [Id(9)] public string Status { get; set; }
    [Id(10)] public string BalanceAmount { get; set; }
    [Id(11)] public string MinAmount { get; set; }
}