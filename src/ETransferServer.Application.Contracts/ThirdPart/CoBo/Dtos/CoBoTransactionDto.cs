using Newtonsoft.Json;
using Orleans;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

// https://www.cobo.com/developers/api-references/custody-wallet/transactions_by_time_ex
[GenerateSerializer]
public class CoBoTransactionDto
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string Coin { get; set; }
    [Id(2)] [JsonProperty("display_code")] public string DisplayCode { get; set; }
    [Id(3)] public string Description { get; set; }
    [Id(4)] public int Decimal { get; set; }
    [Id(5)] public string Address { get; set; }
    [Id(6)] public string Memo { get; set; }
    [Id(7)] [JsonProperty("source_address")] public string SourceAddress { get; set; }
    [Id(8)] public string Side { get; set; }
    [Id(9)] public string Amount { get; set; }
    [Id(10)] [JsonProperty("abs_amount")] public string AbsAmount { get; set; }
    [Id(11)] [JsonProperty("txid")] public string TxId { get; set; }
    [Id(12)] [JsonProperty("vout_n")] public int VoutN { get; set; }
    [Id(13)] [JsonProperty("request_id")] public string RequestId { get; set; }
    [Id(14)] public string Status { get; set; }
    [Id(15)] [JsonProperty("created_time")] public long CreatedTime { get; set; }
    [Id(16)] [JsonProperty("last_time")] public long LastTime { get; set; }
    [Id(17)] public string Remark { get; set; }
    [Id(18)] [JsonProperty("confirmed_num")] public int ConfirmedNum { get; set; }
    [Id(19)] [JsonProperty("confirming_threshold")] public int ConfirmingThreshold { get; set; }
    [Id(20)] [JsonProperty("abs_cobo_fee")] public string AbsCoBoFee { get; set; }
    [Id(21)] [JsonProperty("fee_coin")] public string FeeCoin { get; set; }
    [Id(22)] [JsonProperty("fee_amount")] public string FeeAmount { get; set; }
    [Id(23)] [JsonProperty("fee_decimal")] public string FeeDecimal { get; set; }
    [Id(24)] [JsonProperty("tx_detail")] public TransactionDetailDto TxDetail { get; set; }
}

[GenerateSerializer]
public class TransactionDetailDto
{
    [Id(0)] public string TxId { get; set; }
    [Id(1)] public int BlockNum { get; set; }
    [Id(2)] [JsonProperty("confirming_threshold")] public int ConfirmingThreshold { get; set; }
    [Id(3)] public string BlockHash { get; set; }
    [Id(4)] public string HexStr { get; set; }
}