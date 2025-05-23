using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class AddChainInput
{
    [Required] public string Symbol { get; set; }
    public List<string>? OtherChainIds { get; set; }
    public List<string>? ChainIds { get; set; }
}