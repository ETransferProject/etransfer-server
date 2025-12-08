using System;
using System.Collections.Generic;

namespace ETransferServer.Dtos.GraphQL;

public class SwapRecordDto
{
    public string TransactionId { get; set; }
    public string SymbolIn { get; set; }
    public string SymbolOut { get; set; }
    public long AmountIn { get; set; }
    public long AmountOut { get; set; }
    public string FromAddress { get; set; }
    public string ToAddress { get; set; }
    public List<string> SwapPath { get; set; }
    public string Channel { get; set; }
    public long FeeRate { get; set; }
    public DateTime Timestamp { get; set; }
    public long BlockHeight { get; set; }
}