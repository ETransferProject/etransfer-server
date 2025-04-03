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

public class TonApiMemoResponse : TonResponse
{
    [JsonProperty("out_msgs")] public List<OutMsg> OutMsgs { get; set; }
}

public class OutMsg
{
    [JsonProperty("decoded_body")] public DecodedBody DecodedBody { get; set; }
}

public class DecodedBody
{
    [JsonProperty("forward_payload")] public ForwardPayload ForwardPayload { get; set; }
}

public class ForwardPayload
{
    [JsonProperty("value")] public PayloadValue Value { get; set; }
}

public class PayloadValue
{
    [JsonProperty("sum_type")] public string SumType { get; set; }
    [JsonProperty("value")] public string Value { get; set; }
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