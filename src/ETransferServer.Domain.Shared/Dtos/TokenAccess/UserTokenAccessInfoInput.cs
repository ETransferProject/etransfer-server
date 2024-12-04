using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class UserTokenAccessInfoBaseInput
{
    [Required] public string Symbol { get; set; }
}

public class UserTokenAccessInfoInput : UserTokenAccessInfoBaseInput
{
    public string? OfficialWebsite { get; set; }
    public string? OfficialTwitter { get; set; }
    public string? Title { get; set; }
    public string? PersonName { get; set; }
    public string? TelegramHandler { get; set; }
    public string? Email { get; set; }
}