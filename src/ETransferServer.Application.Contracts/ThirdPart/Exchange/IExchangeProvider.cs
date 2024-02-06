using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.Token;

namespace ETransferServer.ThirdPart.Exchange;

public interface IExchangeProvider
{
    public ExchangeProviderName Name();

    public Task<TokenExchangeDto> LatestAsync(string fromSymbol, string toSymbol);

    public Task<List<TokenExchangeDto>> LatestAsync(List<string> fromSymbol, string toSymbol);

    public Task<TokenExchangeDto> HistoryAsync(string fromSymbol, string toSymbol, long timestamp);

}


public enum ExchangeProviderName
{
    Binance,
    Okx,
    CoinGecko,
}