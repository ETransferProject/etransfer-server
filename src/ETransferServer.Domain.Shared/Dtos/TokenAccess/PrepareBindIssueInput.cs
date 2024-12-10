using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class PrepareBindIssueInput
{
    [Required] public string Address { get; set; }
    [Required] public string Symbol { get; set; }
    public string? ChainId { get; set; }
    public string? OtherChainId { get; set; }
    [Required] public string ContractAddress { get; set; }
    [Required] public string Supply { get; set; }
}