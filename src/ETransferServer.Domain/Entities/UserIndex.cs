using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using Nest;

namespace ETransferServer.Entities;

public class UserIndex : AbstractEntity<Guid>, IIndexBuild
{
    [Keyword]  public override Guid Id { get; set; }
    [Keyword] public string AppId { get; set; }
    [Keyword] public Guid UserId { get; set; }
    [Keyword]  public string CaHash { get; set; }
    public List<UserAddressInfo> AddressInfos { get; set; }
    public long CreateTime { get; set; }
    public long ModificationTime { get; set; }
}

public class UserAddressInfo
{
    [Keyword] public string ChainId { get; set; }
    [Keyword] public string Address { get; set; }
}