using System.Threading.Tasks;
using ETransferServer.Options;

namespace ETransferServer.Token;

public interface ITokenInfoProvider
{
    Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol);
}

public class TokenInfoProvider : ITokenInfoProvider
{
    private readonly TokenInfoOptions _tokenInfoOptions;

    public TokenInfoProvider(TokenInfoOptions tokenInfoOptions)
    {
        _tokenInfoOptions = tokenInfoOptions;
    }

    public Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol)
    {
        if (_tokenInfoOptions.Tokens.TryGetValue(chainId, out var tokenMap) &&
            tokenMap.TryGetValue(symbol, out var tokenInfo))
        {
            return Task.FromResult(tokenInfo);
        }

        return Task.FromResult<TokenInfoDto>(null);
    }
}