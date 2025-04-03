namespace ETransferServer.WithdrawOrder.Dtos;

public class CreateTransferOrderDto : CreateWithdrawOrderDto
{
    public string Address { get; set; }
}