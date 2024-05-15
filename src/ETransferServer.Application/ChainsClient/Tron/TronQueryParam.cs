using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Tron;

public class TronQueryParam
{
    [JsonProperty("value")] public string Value { get; set; }
    [JsonProperty("visible")] public bool Visible { get; set; }
}