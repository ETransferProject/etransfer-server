using System.Collections.Generic;

namespace ETransferServer.Token.Dtos;

public class GetTokenPriceListRequestDto
{
    public List<string> Symbols { get; set; }
}

public class TokenPriceDataDto
{
    public string Symbol { get; set; }
    public decimal PriceUsd { get; set; }
}