using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Models;

public class GetTransferOrderInfoRequestDto : IValidatableObject
{
    [Required] public string FromNetwork { get; set; }
    [Required] public string ToNetwork { get; set; }
    [Required] public string FromSymbol { get; set; }
    [Required] public string ToSymbol { get; set; }
    [Required] public string FromAddress { get; set; }
    [Required] public string ToAddress { get; set; }
    public string? Address { get; set; }
    public string? TxId { get; set; }
    public string? Status { get; set; }
    public decimal Amount { get; set; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Amount <= 0)
        {
            yield return new ValidationResult(
                "Invalid input.",
                new[] { "Amount" }
            );
        }
    }
}