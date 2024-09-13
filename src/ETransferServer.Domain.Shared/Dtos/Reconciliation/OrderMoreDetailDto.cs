using ETransferServer.Dtos.Order;

namespace ETransferServer.Dtos.Reconciliation;

public class OrderMoreDetailDto : OrderDetailDto
{
    public string RelatedOrderId { get; set; }
    public FeeInfo ThirdPartFee { get; set; }
}