using System;
using Orleans;

namespace ETransferServer.Dtos.User;

[GenerateSerializer]
public class UserAddressDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string UserId { get; set; }
    [Id(2)] public string ChainId { get; set; }
    [Id(3)] public TokenDto UserToken { get; set; }
    [Id(4)] public bool IsAssigned { get; set; }
    [Id(5)] public string FromSymbol { get; set; }
    [Id(6)] public string ToSymbol { get; set; }
    [Id(7)] public long UpdateTime { get; set; }
    [Id(8)] public long CreateTime { get; set; }
    [Id(9)] public string OrderId { get; set; }
}