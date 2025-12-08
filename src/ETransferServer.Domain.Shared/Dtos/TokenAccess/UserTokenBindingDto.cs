using System.ComponentModel.DataAnnotations;
using Orleans;

namespace ETransferServer.Dtos.TokenAccess;

[GenerateSerializer]
public class UserTokenBindingDto
{
    [Id(0)] [Required] public string BindingId { get; set; }
    [Id(1)] [Required] public string ThirdTokenId { get; set; }
}