using System.Collections.Generic;
using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Ton;

public class TonResponse
{
}

public class TonScanResponse : TonResponse
{
    [JsonProperty("json")] public TonScanJson Json { get; set; }
}

public class TonScanJson {
    [JsonProperty("status")] public string Status { get; set; }
    [JsonProperty("data")] public TonScanData Data { get;set; }
}
public class TonScanData
{
    [JsonProperty("transactions")] public Dictionary<string, TonScanItem> Transactions { get; set; }
}

public class TonScanItem
{
    [JsonProperty("utime")] public long Utime { get; set; }
}

public class TonCenterResponse : TonResponse
{
    [JsonProperty("transactions")] public List<TonCenterItem> Transactions { get; set; }
}

public class TonCenterItem
{
    [JsonProperty("hash")] public string Hash { get; set; }
    [JsonProperty("now")] public long Now { get; set; }
}

public class TonApiResponse : TonResponse
{
    [JsonProperty("utime")] public long Utime { get; set; }
}