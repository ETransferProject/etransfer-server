using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class PrepareBindIssueInput
{
    [Required] public string address { get; set; }
    [Required] public string Symbol { get; set; }
    [Required] public string ChainId { get; set; }
    [Required] public string OtherChainId { get; set; }
}