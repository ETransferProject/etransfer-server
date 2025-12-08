using System.ComponentModel.DataAnnotations;
using ETransferServer.Dtos.User;

namespace ETransferServer.User;

public class UserAddressCreateInput
{
    [Required]
    public string UserId { get; set; }
    [Required]
    public TokenDto MainToken { get; set; }
    [Required]
    public TokenDto SideToken { get; set; }
}