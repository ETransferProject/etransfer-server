using System.Collections.Generic;

namespace ETransferServer.Dtos.Token;

public class TokenExchangeDto
{
    
    public string FromSymbol { get; set; }
    public string ToSymbol { get; set; }
    public decimal Exchange { get; set; }
    public long Timestamp { get; set; }
    
}