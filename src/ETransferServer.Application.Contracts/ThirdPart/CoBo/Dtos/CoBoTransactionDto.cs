using Newtonsoft.Json;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

// https://www.cobo.com/developers/api-references/custody-wallet/transactions_by_time_ex
public class CoBoTransactionDto
{
    public string Id { get; set; }
    public string Coin { get; set; }
    [JsonProperty("display_code")] public string DisplayCode { get; set; }
    public string Description { get; set; }
    public int Decimal { get; set; }
    public string Address { get; set; }
    public string Memo { get; set; }
    [JsonProperty("source_address")] public string SourceAddress { get; set; }
    public string Side { get; set; }
    public string Amount { get; set; }
    [JsonProperty("abs_amount")] public string AbsAmount { get; set; }
    [JsonProperty("txid")] public string TxId { get; set; }
    [JsonProperty("vout_n")] public int VoutN { get; set; }
    [JsonProperty("request_id")] public string RequestId { get; set; }
    public string Status { get; set; }
    [JsonProperty("created_time")] public long CreatedTime { get; set; }
    [JsonProperty("last_time")] public long LastTime { get; set; }
    public string Remark { get; set; }
    [JsonProperty("confirmed_num")] public int ConfirmedNum { get; set; }
    [JsonProperty("confirming_threshold")] public int ConfirmingThreshold { get; set; }
    
    [JsonProperty("abs_cobo_fee")] public string AbsCoBoFee { get; set; }
    [JsonProperty("fee_coin")] public string FeeCoin { get; set; }
    [JsonProperty("fee_amount")] public string FeeAmount { get; set; }
    [JsonProperty("fee_decimal")] public string FeeDecimal { get; set; }
    [JsonProperty("tx_detail")] public TransactionDetailDto TxDetail { get; set; }
}

public class TransactionDetailDto
{
    public string TxId { get; set; }
    public int BlockNum { get; set; }
    public string BlockHash { get; set; }
    public string HexStr { get; set; }
}