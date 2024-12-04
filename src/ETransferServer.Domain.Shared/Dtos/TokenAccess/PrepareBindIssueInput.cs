using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class PrepareBindIssueInput
{
    [Required] public string Address { get; set; }
    [Required] public string Symbol { get; set; }
    [Required] public string ChainId { get; set; }
    [Required] public string OtherChainId { get; set; }
    [Required] public string Supply { get; set; }
}