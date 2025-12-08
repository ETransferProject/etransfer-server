using Orleans;

namespace ETransferServer.Dtos.GraphQL;

[GenerateSerializer]
public class TransferDto
{
    [Id(0)] public string TransactionId { get; set; } 
    [Id(1)] public string ChainId { get; set; } 
    [Id(2)] public string BlockHash { get; set; } 
    [Id(3)] public long BlockHeight { get; set; }
    [Id(4)] public string Amount { get; set; } 
    [Id(5)] public string Symbol { get; set; } 
    [Id(6)] public string FromAddress { get; set; } 
    [Id(7)] public string ToAddress { get; set; } 
    [Id(8)] public string From { get; set; } // Sender 
    [Id(9)] public string To { get; set; } // Contract address
    [Id(10)] public string Status { get; set; }

}