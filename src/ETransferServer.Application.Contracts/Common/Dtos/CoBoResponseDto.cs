using Newtonsoft.Json;

namespace ETransferServer.Common.Dtos;

public class CoBoResponseDto<T>
{
    public bool Success { get; set; }
    public T Result { get; set; }
}

public class CoBoResponseDto
{
    public bool Success { get; set; }
}

public class CoBoResponseErrorDto : CoBoResponseDto
{
    [JsonProperty("error_code")] public int ErrorCode { get; set; }
    [JsonProperty("error_message")] public string ErrorMessage { get; set; }
    [JsonProperty("error_id")] public string ErrorId { get; set; }
    [JsonProperty("error_description")] public string ErrorDescription { get; set; }
}