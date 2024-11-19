using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace ETransferServer.Dtos.Order;

public class GetOrderRecordRequestDto : PagedAndSortedResultRequestDto
{
    [Range(0, 2)]
    [Required]
    public int? Type { get; set; }
    [Range(0, 3)]
    [Required]
    public int? Status { get; set; }
    public long? StartTimestamp { get; set; }
    public long? EndTimestamp { get; set; }
    public List<string>? AddressList { get; set; }
}