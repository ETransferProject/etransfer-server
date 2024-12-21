using System.Collections.Generic;
using Newtonsoft.Json;
using Orleans;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

[GenerateSerializer]
public class AccountDetailDto
{
    [Id(0)] public string Name { get; set; }
    [Id(1)] public List<AssetDto> Assets { get; set; }
}

[GenerateSerializer]
public class AssetDto
{
    [Id(0)] public string Coin { get; set; }
    [Id(1)] [JsonProperty("display_code")] public string DisplayCode { get; set; }
    [Id(2)] public string Description { get; set; }
    [Id(3)] public int Decimal { get; set; }
    [Id(4)] [JsonProperty("can_deposit")] public bool CanDeposit { get; set; }
    [Id(5)] [JsonProperty("can_withdraw")] public bool CanWithdraw { get; set; }
    [Id(6)] public string Balance { get; set; }
    [Id(7)] [JsonProperty("abs_balance")] public string AbsBalance { get; set; }
    [Id(8)] [JsonProperty("fee_coin")] public string FeeCoin { get; set; }
    [Id(9)] [JsonProperty("abs_estimate_fee")] public string AbsEstimateFee { get; set; }
    [Id(10)] [JsonProperty("confirming_threshold")] public int ConfirmingThreshold { get; set; }
    [Id(11)] [JsonProperty("dust_threshold")] public int DustThreshold { get; set; }
    [Id(12)] [JsonProperty("token_address")] public string TokenAddress { get; set; }
    [Id(13)] [JsonProperty("require_memo")] public bool RequireMemo { get; set; }
}