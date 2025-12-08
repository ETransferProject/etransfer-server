using Orleans;

namespace ETransferServer.Dtos.GraphQL;

[GenerateSerializer]
public class TransferRecordDto
{
    [Id(0)] public string Id { get; set; }
    [Id(1)] public string TransactionId { get; set; }
    [Id(2)] public string MethodName { get; set; }
    [Id(3)] public string From { get; set; }
    [Id(4)] public string To { get; set; }
    [Id(5)] public string ToChainId { get; set; }
    [Id(6)] public string ToAddress { get; set; }
    [Id(7)] public string Symbol { get; set; }
    [Id(8)] public long Amount { get; set; }
    [Id(9)] public long MaxEstimateFee { get; set; }
    [Id(10)] public long Timestamp { get; set; }
    [Id(11)] public string TransferType { get; set; }
    [Id(12)] public string ChainId { get; set; }
    [Id(13)] public string BlockHash { get; set; }
    [Id(14)] public long BlockHeight { get; set; }
    [Id(15)] public string Memo { get; set; }
}