using System.ComponentModel.DataAnnotations;
using Orleans;

namespace ETransferServer.User;

[GenerateSerializer]
public class GetUserDepositAddressInput
{
    [Id(0)] public string UserId { get; set; }
    [Id(1)] [Required] public string ChainId { get; set; }
    [Id(2)] [Required] public string NetWork { get; set; }
    [Id(3)] public string Symbol { get; set; }
    [Id(4)] public string ToSymbol { get; set; }
}