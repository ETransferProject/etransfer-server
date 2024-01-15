using System;
using System.Collections.Generic;

namespace ETransferServer.User.Dtos;

public class UserDto
{
    public Guid Id { get; set; }
    public string AppId { get; set; }
    public Guid UserId { get; set; }
    public string CaHash { get; set; }
    public List<AddressInfo> AddressInfos { get; set; }
    public long CreateTime { get; set; }
    public long ModificationTime { get; set; }
}

public class AddressInfo
{
    public string ChainId { get; set; }
    public string Address { get; set; }
}