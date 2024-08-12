using System.Collections.Generic;
using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Ton;

public class TonResponse
{
}

public class TonApiResponse : TonResponse
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