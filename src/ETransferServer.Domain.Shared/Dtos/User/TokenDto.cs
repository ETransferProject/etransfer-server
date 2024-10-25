using System;
using Orleans;

namespace ETransferServer.Dtos.User;

[GenerateSerializer]
public class TokenDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string ChainId { get; set; }
    [Id(2)] public string Address { get; set; }
    [Id(3)] public string Symbol { get; set; }
    [Id(4)] public int Decimals { get; set; }
}