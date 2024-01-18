using System.ComponentModel.DataAnnotations;

namespace ETransferServer.User;

public class GetUserDepositAddressInput
{
    public string UserId { get; set; }
    [Required] public string ChainId { get; set; }
    [Required] public string NetWork { get; set; }
    public string Symbol { get; set; }
}