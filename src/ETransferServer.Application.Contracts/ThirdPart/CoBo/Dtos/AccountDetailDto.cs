using System.Collections.Generic;
using Newtonsoft.Json;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

public class AccountDetailDto
{
    public string Name { get; set; }
    public List<AssetDto> Assets { get; set; }
}

public class AssetDto
{
    public string Coin { get; set; }
    [JsonProperty("display_code")] public string DisplayCode { get; set; }
    public string Description { get; set; }
    public int Decimal { get; set; }
    [JsonProperty("can_deposit")] public bool CanDeposit { get; set; }
    [JsonProperty("can_withdraw")] public bool CanWithdraw { get; set; }
    public string Balance { get; set; }
    [JsonProperty("abs_balance")] public string AbsBalance { get; set; }
    [JsonProperty("fee_coin")] public string FeeCoin { get; set; }
    [JsonProperty("abs_estimate_fee")] public string AbsEstimateFee { get; set; }
    [JsonProperty("confirming_threshold")] public int ConfirmingThreshold { get; set; }
    [JsonProperty("dust_threshold")] public int DustThreshold { get; set; }
    [JsonProperty("token_address")] public string TokenAddress { get; set; }
    [JsonProperty("require_memo")] public bool RequireMemo { get; set; }
}