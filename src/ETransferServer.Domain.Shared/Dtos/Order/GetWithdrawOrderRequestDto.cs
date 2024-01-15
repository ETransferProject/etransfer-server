using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Models;

public class GetWithdrawOrderRequestDto : IValidatableObject
{
    [Required] public string Network { get; set; }
    [Required] public string Symbol { get; set; }
    [Required] public string RawTransaction { get; set; }
    [Required] public string FromChainId { get; set; }
    [Required] public string ToAddress { get; set; }

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