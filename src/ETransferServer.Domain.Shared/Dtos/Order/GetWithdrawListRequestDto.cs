using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Order;

public class GetWithdrawListRequestDto : IValidatableObject
{
    [Required] public string ChainId { get; set; }
    public string? Network { get; set; }
    [Required] public string Symbol { get; set; }
    public decimal Amount { get; set; }
    public string? Address { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        int decimalPlaces = Amount.ToString().Length - Amount.ToString().IndexOf('.') - 1;
        if (decimalPlaces > 6 || Amount < 0)
        {
            yield return new ValidationResult(
                "Amount invalid.",
                new[] { "Amount" }
            );
        }
    }
}