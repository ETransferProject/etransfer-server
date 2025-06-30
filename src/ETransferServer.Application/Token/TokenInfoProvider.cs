using System.Threading.Tasks;
using ETransferServer.Options;
using Microsoft.Extensions.Options;

namespace ETransferServer.Token;

public interface ITokenInfoProvider
{
    Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol);
}

public class TokenInfoProvider : ITokenInfoProvider
{
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;

    public TokenInfoProvider(IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions)
    {
        _tokenInfoOptions = tokenInfoOptions;
    }

    public Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol)
    {
        if (_tokenInfoOptions.Value.Tokens.TryGetValue(chainId, out var tokenMap) &&
            tokenMap.TryGetValue(symbol, out var tokenInfo))
        {
            return Task.FromResult(tokenInfo);
        }

        return Task.FromResult<TokenInfoDto>(null);
    }
}