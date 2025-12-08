using System;
using Nest;
using ETransferServer.Entities;

namespace ETransferServer.Tokens;

public class Token : AbstractEntity<Guid>
{
    [Keyword] public override Guid Id { get; set; }
    [Keyword] public virtual string ChainId { get; set; }
    [Keyword] public virtual string Address { get; set; }
    [Keyword] public virtual string Symbol { get; set; }
    public virtual int Decimals { get; set; }
}