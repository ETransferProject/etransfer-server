using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Tron;

public class TronResponse
{
    [JsonProperty("txID")] public string TxId { get; set; }
    [JsonProperty("raw_data")] public RawData RawData { get; set; }
}

public class RawData
{
    [JsonProperty("timestamp")] public long TimeStamp { get; set; }
}
