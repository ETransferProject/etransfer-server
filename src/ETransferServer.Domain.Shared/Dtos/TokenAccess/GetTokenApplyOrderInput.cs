using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.TokenAccess;

public class GetTokenApplyOrderInput
{
    [Required] public string Symbol { get; set; }
    public string? Id { get; set; }
    public string? Network { get; set; }
}