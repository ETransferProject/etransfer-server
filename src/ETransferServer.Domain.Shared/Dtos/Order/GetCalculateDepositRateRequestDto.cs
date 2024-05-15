
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Models;

public class GetCalculateDepositRateRequestDto : IValidatableObject
{
    public string ToChainId { get; set; }
    public string FromSymbol { get; set; }
    public string ToSymbol { get; set; }
    public decimal FromAmount { get; set; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        int decimalPlaces = FromAmount.ToString().Length - FromAmount.ToString().IndexOf('.') - 1;
        if (decimalPlaces > 8 || FromAmount < 0)
        {
            yield return new ValidationResult(
                "Amount invalid.",
                new[] { "Amount" }
            );
        }
    }
}