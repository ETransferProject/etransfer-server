using ETransferServer.ThirdPart.CoBo.Dtos;

namespace ETransferServer.Grains.State.Token;

[GenerateSerializer]
public class CoBoAccountState : AccountDetailDto
{
    [Id(0)] public long LastModifyTime { get; set; }
    [Id(1)] public long ExpireTime { get; set; }
}