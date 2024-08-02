namespace ETransferServer.Token.Dtos;

public class GetTokenPriceListRequestDto
{
    public string Symbols { get; set; }
}

public class TokenPriceDataDto
{
    public string Symbol { get; set; }
    public decimal PriceUsd { get; set; }
}