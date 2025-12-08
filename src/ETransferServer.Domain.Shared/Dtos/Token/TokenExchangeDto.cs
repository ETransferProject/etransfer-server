using Orleans;

namespace ETransferServer.Dtos.Token;

[GenerateSerializer]
public class TokenExchangeDto
{
    
    [Id(0)] public string FromSymbol { get; set; }
    [Id(1)] public string ToSymbol { get; set; }
    [Id(2)] public decimal Exchange { get; set; }
    [Id(3)] public long Timestamp { get; set; }


    public static TokenExchangeDto One(string fromSymbol, string toSymbol, long timestamp)
    {
        return new TokenExchangeDto
        {
            FromSymbol = fromSymbol,
            ToSymbol = toSymbol,
            Exchange = 1,
            Timestamp = timestamp
        };
    }
    
}