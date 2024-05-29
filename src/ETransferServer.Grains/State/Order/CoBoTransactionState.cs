using ETransferServer.ThirdPart.CoBo.Dtos;

namespace ETransferServer.Grains.State.Order;

public class CoBoTransactionState : CoBoTransactionDto
{
    public int UpdateCount { get; set; } = -1;
}