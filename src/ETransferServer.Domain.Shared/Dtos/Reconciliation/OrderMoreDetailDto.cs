using ETransferServer.Dtos.Order;

namespace ETransferServer.Dtos.Reconciliation;

public class OrderMoreDetailDto : OrderDetailDto
{
    public string RelatedOrderId { get; set; }
    public FeeInfo ThirdPartFee { get; set; }
    public int RoleType { get; set; } = -1;
    public string OperationStatus { get; set; }
}