namespace ETransferServer.Dtos.GraphQL;

public class TransferRecordDto
{
    public string Id { get; set; }
    public string TransactionId { get; set; }
    public string MethodName { get; set; }
    public string From { get; set; }
    public string To { get; set; }
    public string ToChainId { get; set; }
    public string ToAddress { get; set; }
    public string Symbol { get; set; }
    public long Amount { get; set; }
    public long MaxEstimateFee { get; set; }
    public long Timestamp { get; set; }
    public string TransferType { get; set; }
    public string ChainId { get; set; }
    public string BlockHash { get; set; }
    public long BlockHeight { get; set; }
}