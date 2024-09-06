using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Order;

public class GetUserOrderRecordRequestDto
{
    [Required]
    public string Address { get; set; }
    public long? Time { get; set; }
}