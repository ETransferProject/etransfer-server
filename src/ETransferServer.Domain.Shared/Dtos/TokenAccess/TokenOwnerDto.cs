using System.Collections.Generic;
using System.Linq;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class TokenOwnerListDto
{
    [Id(0)] public List<TokenOwnerDto> TokenOwnerList { get; set; } = new();
}

[GenerateSerializer]
public class TokenOwnerDto
{
    [Id(0)] public string TokenName { get; set; }
    [Id(1)] public string Symbol { get; set; }
    [Id(2)] public int Decimals { get; set; }
    [Id(3)] public string Icon { get; set; }
    [Id(4)] public string Owner { get; set; }
    [Id(5)] public List<string> ChainIds { get; set; } = new();
    [Id(6)] public decimal TotalSupply { get; set; }
    [Id(7)] public int Holders { get; set; }
    [Id(8)] public string PoolAddress { get; set; }
    [Id(9)] public string ContractAddress { get; set; }
    [Id(10)] public string Status { get; set; }
    
    public override bool Equals(object obj)
    {
        if (obj is TokenOwnerDto t)
        {
            return Symbol == t.Symbol && new HashSet<string>(ChainIds).SetEquals(t.ChainIds);
        }
        return false;
    }

    public override int GetHashCode()
    {
        return Symbol.GetHashCode() ^
               new HashSet<string>(ChainIds).Aggregate(0, (acc, chainId) => acc ^ chainId.GetHashCode());
    }
}