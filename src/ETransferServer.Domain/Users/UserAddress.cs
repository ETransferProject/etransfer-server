using System;
using AElf.Indexing.Elasticsearch;
using Nest;
using Token = ETransferServer.Tokens.Token;

namespace ETransferServer.Users;

public class UserAddress : UserAddressBase<Guid>, IIndexBuild
{
    [Keyword] public override Guid Id { get; set; }
    [Keyword] public string UserId { get; set; }
    [Keyword] public string ChainId { get; set; }
    public Token UserToken { get; set; }
    public bool IsAssigned { get; set; }
    [Keyword] public string FromSymbol { get; set; }
    [Keyword] public string ToSymbol { get; set; }
    public long UpdateTime { get; set; }
    public long CreateTime { get; set; }
}