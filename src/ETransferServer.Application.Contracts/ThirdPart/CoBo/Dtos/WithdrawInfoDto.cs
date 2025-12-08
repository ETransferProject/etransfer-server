using Newtonsoft.Json;

namespace ETransferServer.ThirdPart.CoBo.Dtos;

public class WithdrawInfoDto : CoBoTransactionDto
{
    [JsonProperty("source_address_detail")]
    public string SourceAddressDetail { get; set; }
    [JsonProperty("confirming_threshold")]
    public string ConfirmingThreshold { get; set; }
    public string Type { get; set; }
}