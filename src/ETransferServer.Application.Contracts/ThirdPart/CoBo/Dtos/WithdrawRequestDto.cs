using Newtonsoft.Json;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

public class WithdrawRequestDto
{
    public string Coin { get; set; }
    [JsonProperty("request_id")]
    public string RequestId { get; set; }
    public string Address { get; set; }
    public string Amount { get; set; }
    /// <summary>
    /// Needed when you withdraw EOS, XRP, IOST
    /// </summary>
    public string Memo { get; set; }
    /// <summary>
    /// The remark to withdraw.
    /// </summary>
    public string Remark { get; set; }
    [JsonProperty("force_external")]
    public string ForceExternal { get; set; }
    [JsonProperty("force_external")]
    public string ForceInternal { get; set; }
}