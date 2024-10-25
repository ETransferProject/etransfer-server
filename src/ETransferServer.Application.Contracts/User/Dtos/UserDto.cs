using System;
using System.Collections.Generic;
using Orleans;

namespace ETransferServer.User.Dtos;

[GenerateSerializer]
public class UserDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string AppId { get; set; }
    [Id(2)] public Guid UserId { get; set; }
    [Id(3)] public string CaHash { get; set; }
    [Id(4)] public List<AddressInfo> AddressInfos { get; set; }
    [Id(5)] public long CreateTime { get; set; }
    [Id(6)] public long ModificationTime { get; set; }
}

[GenerateSerializer]
public class AddressInfo
{
    [Id(0)] public string ChainId { get; set; }
    [Id(1)] public string Address { get; set; }
}