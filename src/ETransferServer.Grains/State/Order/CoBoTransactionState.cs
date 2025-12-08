using ETransferServer.ThirdPart.CoBo.Dtos;

namespace ETransferServer.Grains.State.Order;

[GenerateSerializer]
public class CoBoTransactionState : CoBoTransactionDto
{
    [Id(0)] public int UpdateCount { get; set; } = -1;
}