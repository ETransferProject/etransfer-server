namespace ETransferServer.Dtos.GraphQL;

public class TransferDto
{
    
    public string TransactionId { get; set; } 
    public string ChainId { get; set; } 
    public string BlockHash { get; set; } 
    public long BlockHeight { get; set; }
    public string Amount { get; set; } 
    public string Symbol { get; set; } 
    public string FromAddress { get; set; } 
    public string ToAddress { get; set; } 
    public string From { get; set; } // Sender 
    public string To { get; set; } // Contract address
    public string Status { get; set; }

}