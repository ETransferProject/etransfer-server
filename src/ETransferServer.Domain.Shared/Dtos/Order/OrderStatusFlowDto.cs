using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace ETransferServer.Dtos.Order;

public class OrderStatusFlowDto
{
    public Guid Id { get; set; }
    public List<OrderStatus> StatusFlow { get; set; } = new();
    
}

public class OrderStatus
{
    public string Status { get; set; }
    public long LastModifyTime { get; set; }
    public Dictionary<string, string> Extension { get; set; }
}

public static class ExtensionKey
{
    public const string Transaction = "Tx";
    public const string TransactionId = "TxId";
    public const string TransactionStatus = "TxStatus";
    public const string TransactionError = "TxErr";
    public const string RequestId = "requestId";
    public const string ToTransferTxId = "toTransferTxId";
    public const string SwapTxId = "swapTxId";
    public const string SwapSubsidyTxId = "SwapSubsidyTxId";
    public const string NeedSwap = "needSwap";
    public const string SwapStage = "SwapStage";
    public const string SwapSubsidyTxTime = "SwapSubsidyTxTime";
}

public static class SwapStage
{
    public const string SwapTx = "SwapTx";
    public const string SwapSubsidy = "SwapSubsidy";
}

public class ExtensionBuilder
{
    private Dictionary<string, string> _dict = new();

    public static ExtensionBuilder New()
    {
        return new ExtensionBuilder();
    }
    
    public Dictionary<string, string> Build()
    {
        return _dict;
    }

    public ExtensionBuilder Add(string key, object? data)
    {
        if (data == null)
        {
            return this;
        }

        // Check for different number types
        _dict[key] = data switch
        {
            string str => str,
            int num => num.ToString(),
            long num => num.ToString(),
            float num => num.ToString(CultureInfo.InvariantCulture),
            double num => num.ToString(CultureInfo.InvariantCulture),
            decimal num => num.ToString(CultureInfo.InvariantCulture),
            // Add other numeric types if needed
            _ => JsonConvert.SerializeObject(data)
        };

        return this;
    }
}