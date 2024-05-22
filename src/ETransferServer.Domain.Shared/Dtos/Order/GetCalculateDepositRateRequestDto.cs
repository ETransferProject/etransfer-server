
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
        if (FromAmount != null)
        {
            string fromAmountStr = FromAmount.ToString();
            int decimalIndex = fromAmountStr.IndexOf('.');
            if (decimalIndex != -1)
            {
                int decimalPlaces = fromAmountStr.Length - decimalIndex - 1;
                if (decimalPlaces > 6 || FromAmount < 0)
                {
                    yield return new ValidationResult(
                        "FromAmount invalid.",
                        new[] { "FromAmount" }
                    );
                }
            }
        }
    }
}