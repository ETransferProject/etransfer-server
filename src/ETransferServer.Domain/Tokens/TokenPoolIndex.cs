using System;
using System.Collections.Generic;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Entities;
using Nest;

namespace ETransferServer.Tokens;

public class TokenPoolIndex : AbstractEntity<Guid>, IIndexBuild
{
    [Keyword] public override Guid Id { get; set; }
    [Keyword] public string Date { get; set; }
    public Dictionary<string, string> MultiPool { get; set; } = new();
    public Dictionary<string, string> TokenPool { get; set; } = new();
    public Dictionary<string, string> ThirdFeeInfo { get; set; } = new();
    public Dictionary<string, string> ThirdPoolFeeInfo { get; set; } = new();
    public Dictionary<string, string> Pool { get; set; } = new();
    public Dictionary<string, string> WithdrawFeeInfo { get; set; } = new();
    public Dictionary<string, string> DepositFeeInfo { get; set; } = new();
}