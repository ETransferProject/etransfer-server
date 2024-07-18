using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Info;

public class GetTokenRequestDto : IValidatableObject
{
    [Required]
    public string Type { get; set; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!int.TryParse(Type, out int val) || val < 0 || val > 2)
        {
            yield return new ValidationResult(
                "The field Type must be between 0 and 2.",
                new[] { "Type" }
            );
        }
    }
}