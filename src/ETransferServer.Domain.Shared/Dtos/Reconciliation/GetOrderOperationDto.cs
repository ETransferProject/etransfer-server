using System.ComponentModel.DataAnnotations;

namespace ETransferServer.Dtos.Reconciliation;

public class GetOrderOperationDto
{
    [Required]
    public string OrderId { get; set; }
}

public class GetOrderSafeOperationDto : GetOrderOperationDto
{
    [Required]
    public string Code { get; set; }
}