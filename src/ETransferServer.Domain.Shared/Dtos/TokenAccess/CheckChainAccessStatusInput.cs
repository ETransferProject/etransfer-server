using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class CheckChainAccessStatusInput
{
    [Required] public string Symbol { get; set; }
}