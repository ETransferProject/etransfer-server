using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class GetTokenConfigInput
{
    [Required] public string Symbol { get; set; }
}