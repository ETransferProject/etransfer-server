using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Order;

public class GetOrderRecordRequestDto
{
    [Range(0, 2)]
    [Required]
    public int? Type { get; set; }
    [Range(0, 3)]
    [Required]
    public int? Status { get; set; }
    public long? StartTimestamp { get; set; }
    public long? EndTimestamp { get; set; }
    [Range(0, int.MaxValue)]
    public int? SkipCount { get; set; }
    [Range(1, int.MaxValue)]
    public int? MaxResultCount { get; set; }
    public string? Sorting { get; set; }
    public string? Address { get; set; }
}