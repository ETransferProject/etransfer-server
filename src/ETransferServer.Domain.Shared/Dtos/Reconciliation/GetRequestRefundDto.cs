using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Reconciliation;

public class GetRequestRefundDto
{
    [Required]
    public string FromAddress { get; set; }
    [Required]
    public string Amount { get; set; }
    [Required]
    public string Symbol { get; set; }
    [Required]
    public string ChainId { get; set; }
    [Required]
    public string OrderId { get; set; }
}