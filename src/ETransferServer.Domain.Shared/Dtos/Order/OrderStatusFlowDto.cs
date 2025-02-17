using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Orleans;

namespace ETransferServer.Dtos.Order;

[GenerateSerializer]
public class OrderStatusFlowDto
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public List<OrderStatus> StatusFlow { get; set; } = new();
    
}

[GenerateSerializer]
public class OrderStatus
{
    [Id(0)] public string Status { get; set; }
    [Id(1)] public long LastModifyTime { get; set; }
    [Id(2)] public Dictionary<string, string> Extension { get; set; }
}

public static class ExtensionKey
{
    public const string Transaction = "Tx";
    public const string TransactionId = "TxId";
    public const string TransactionStatus = "TxStatus";
    public const string TransactionError = "TxErr";
    public const string RequestId = "requestId";
    public const string ToTransferTxId = "toTransferTxId";
    public const string IsForward = "isForward";
    public const string Memo = "Memo";
    public const string FromConfirmedNum = "FromConfirmedNum";
    public const string FromConfirmingThreshold = "FromConfirmingThreshold";
    public const string ToConfirmedNum = "ToConfirmedNum";
    public const string RelatedOrderId = "RelatedOrderId";
    public const string RequestUser = "RequestUser";
    public const string RequestTime = "RequestTime";
    public const string ReleaseUser = "ReleaseUser";
    public const string ReleaseTime = "ReleaseTime";
    public const string RefundTx = "RefundTx";
    public const string RefundUser = "RefundUser";
    public const string RefundTime = "RefundTime";
    public const string SubStatus = "SubStatus";
    public const string OrderType = "OrderType";
    
    public const string IsSwap = "IsSwap";
    public const string NeedSwap = "NeedSwap";
    public const string SwapStage = "SwapStage";
    public const string SwapToMain = "SwapToMain";
    public const string SwapFromAddress = "SwapFromAddress";
    public const string SwapOriginFromAddress = "SwapOriginFromAddress";
    public const string SwapToAddress = "SwapToAddress";
    public const string SwapChainId = "SwapChainId";
    public const string SwapTxId = "SwapTxId";
}

public static class SwapStage
{
    public const string SwapTx = "SwapTx";
    public const string SwapTxCheckFailAndToTransfer = "SwapTxCheckFailAndToTransfer";
    public const string SwapTxHandleFailAndToTransfer = "SwapTxHandleFailAndToTransfer";
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