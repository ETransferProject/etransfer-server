using System;

namespace ETransferServer.Dtos.User;

public class UserAddressDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; }
    public string ChainId { get; set; }
    public TokenDto UserToken { get; set; }
    public bool IsAssigned { get; set; }
    public string FromSymbol { get; set; }
    public string ToSymbol { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}