using System;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Options;
using Microsoft.Extensions.Options;

namespace ETransferServer.Token;

public interface ITokenInfoProvider
{
    Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol);
    Task<int> GetTokenDecimalAsync(string chainId, string symbol);
}

public class TokenInfoProvider : ITokenInfoProvider
{
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;

    public TokenInfoProvider(IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions)
    {
        _tokenInfoOptions = tokenInfoOptions;
    }

    public async Task<TokenInfoDto> GetTokenInfoAsync(string chainId, string symbol)
    {
        if (chainId == null)
        {
            foreach (var (_, tokenDic) in _tokenInfoOptions.Value.Tokens)
            {
                if (tokenDic.TryGetValue(symbol, out var token))
                {
                    return token;
                }
            }
            return null;
        }
        if (_tokenInfoOptions.Value.Tokens.TryGetValue(chainId, out var tokenMap) &&
            tokenMap.TryGetValue(symbol, out var tokenInfo))
        {
            return tokenInfo;
        }

        return null;
    }

    /*
     * Return the decimal of the token.
     * If chainId is null, return the decimal of the aelf chain with the given symbol.
     */
    public async Task<int> GetTokenDecimalAsync(string chainId, string symbol)
    {
        if (string.IsNullOrEmpty(chainId))
        {
            foreach (var (chain, tokenDic) in _tokenInfoOptions.Value.Tokens)
            {
                switch (chain)
                {
                    case ChainId.AELF:
                    case ChainId.tDVV:
                    case ChainId.tDVW:
                    {
                        if (tokenDic.TryGetValue(symbol, out var token))
                        {
                            return token.Decimal;
                        }

                        break;
                    }
                    default:
                    {
                        return tokenDic.TryGetValue(symbol, out var token) ? token.Decimal : 0;
                    }
                }
            }
        }
        else
        {
            if (_tokenInfoOptions.Value.Tokens.TryGetValue(chainId, out var tokenMap) &&
                tokenMap.TryGetValue(symbol, out var tokenInfo))
            {
                return tokenInfo.Decimal;
            }
        }

        return 0;
    }
}