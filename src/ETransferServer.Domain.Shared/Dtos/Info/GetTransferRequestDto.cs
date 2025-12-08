using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Info;

public class GetTransferRequestDto : PagedAndSortedResultRequestDto, IValidatableObject
{
    [Required]
    public string Type { get; set; }
    [Range(0, int.MaxValue)]
    public int FromToken { get; set; }
    [Range(0, int.MaxValue)]
    public int FromChainId { get; set; }
    [Range(0, int.MaxValue)]
    public int ToToken { get; set; }
    [Range(0, int.MaxValue)]
    public int ToChainId { get; set; }
    [Range(0, int.MaxValue)]
    public int Limit { get; set; } = 50;
    
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