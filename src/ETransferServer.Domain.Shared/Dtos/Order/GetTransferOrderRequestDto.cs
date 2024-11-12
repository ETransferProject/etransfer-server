using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Models;

public class GetTransferOrderRequestDto : IValidatableObject
{
    [Required] public string FromNetwork { get; set; }
    [Required] public string ToNetwork { get; set; }
    [Required] public string FromSymbol { get; set; }
    [Required] public string ToSymbol { get; set; }
    [Required] public string FromAddress { get; set; }
    [Required] public string ToAddress { get; set; }
    public string? Memo { get; set; }
    public string? RawTransaction { get; set; }
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