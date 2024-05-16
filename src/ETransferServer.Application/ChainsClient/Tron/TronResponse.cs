using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Tron;

public class TronResponse
{
    [JsonProperty("blockID")] public string BlockId { get; set; }
    [JsonProperty("block_header")] public BlockHeader BlockHeader { get; set; }
}

public class BlockHeader {
    [JsonProperty("raw_data")] public RawData RawData { get; set; }
    [JsonProperty("witness_signature")] public string Signature { get;set; }
}
public class RawData
{
    [JsonProperty("timestamp")] public long TimeStamp { get; set; }
}
