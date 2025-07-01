using System.Collections.Generic;
using System.Linq;
using ETransferServer.Common;
using ETransferServer.Options;
using ETransferServer.Token.Dtos;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Token;

public interface ISupportedChainTokenProvider
{
    List<TokenOptionConfigDto> GetTokenConfig(string fromTokenSymbol = null);
    bool IsTokenSupportedDeposit(string fromSymbol, [CanBeNull] string toSymbol, string toChainId);
    bool IsTokenSupportedSwap(string fromSymbol, [CanBeNull] string toSymbol, string toChainId);
    List<TokenConfigDto> GetTokenListByType(string type, [CanBeNull] string chainId);
    bool IsDepositHealth(string symbol, string chainId);
    bool IsWithdrawHealth(string symbol, string chainId);
}

public class SupportedChainTokenProvider : ISupportedChainTokenProvider, ITransientDependency
{
    private readonly IOptionsSnapshot<SupportedTokenSwapOptions> _supportedTokenSwapOptions;
    private readonly IOptionsSnapshot<TokenInfoOptions> _tokenInfoOptions;
    private readonly IOptionsSnapshot<SupportedChainTokensOptions> _supportedChainTokensOptions;

    public SupportedChainTokenProvider(IOptionsSnapshot<SupportedTokenSwapOptions> supportedTokenSwapOptions,
        IOptionsSnapshot<TokenInfoOptions> tokenInfoOptions,
        IOptionsSnapshot<SupportedChainTokensOptions> supportedChainTokensOptions)
    {
        _supportedTokenSwapOptions = supportedTokenSwapOptions;
        _tokenInfoOptions = tokenInfoOptions;
        _supportedChainTokensOptions = supportedChainTokensOptions;
    }

    public List<TokenOptionConfigDto> GetTokenConfig(string fromTokenSymbol = null)
    {
        var result = new List<TokenOptionConfigDto>();
        var swapMap = _supportedTokenSwapOptions.Value.SwapTokenMap;
        var tokens = _tokenInfoOptions.Value.Tokens;

        // 提前构建 symbol -> TokenInfoDto 快速查找字典
        var tokenLookup = new Dictionary<string, TokenInfoDto>();
        foreach (var (_, tokenDic) in tokens)
        {
            foreach (var (symbol, tokenInfo) in tokenDic)
            {
                if (!tokenLookup.ContainsKey(symbol))
                {
                    tokenLookup[symbol] = tokenInfo;
                }
            }
        }

        var symbolsToQuery = string.IsNullOrEmpty(fromTokenSymbol)
            ? swapMap.Keys.ToList()
            : new List<string> { fromTokenSymbol };

        foreach (var symbol in symbolsToQuery)
        {
            if (!swapMap.TryGetValue(symbol, out var toTokenConfigs))
            {
                continue;
            }

            if (!tokenLookup.TryGetValue(symbol, out var tokenInfo))
            {
                continue;
            }

            var tokenConfig = new TokenOptionConfigDto
            {
                Symbol = tokenInfo.Symbol,
                Name = tokenInfo.Name,
                Decimals = tokenInfo.Decimal,
                Icon = tokenInfo.Icon,
                ContractAddress = tokenInfo.TokenAddress,
                ToTokenList = new List<TargetTokenOptionConfigDto>()
            };

            foreach (var toToken in toTokenConfigs)
            {
                if (!tokenLookup.TryGetValue(toToken.Symbol, out var targetTokenInfo))
                {
                    continue;
                }

                var targetDto = new TargetTokenOptionConfigDto
                {
                    Symbol = targetTokenInfo.Symbol,
                    Name = targetTokenInfo.Name,
                    Decimals = targetTokenInfo.Decimal,
                    Icon = targetTokenInfo.Icon,
                    ChainIdList = toToken.ChainIdList
                };

                tokenConfig.ToTokenList.Add(targetDto);
            }

            result.Add(tokenConfig);
        }

        return result;
    }

    public bool IsTokenSupportedDeposit(string fromSymbol, string toSymbol, string toChainId)
    {
        if (DepositSwapHelper.NoDepositSwap(fromSymbol, toSymbol))
        {
            return _supportedTokenSwapOptions.Value.SwapTokenMap.Any(config =>
                config.Key == fromSymbol && config.Value.Any(token =>
                    token.Symbol == fromSymbol && token.ChainIdList.Any(chainId => chainId == toChainId)));
        }

        return DepositSwapHelper.IsDepositSwap(fromSymbol, toSymbol) &&
               IsTokenSupportedSwap(fromSymbol, toSymbol, toChainId);
    }

    public bool IsTokenSupportedSwap(string fromSymbol, string toSymbol, string toChainId)
    {
        var isSwap = DepositSwapHelper.IsDepositSwap(fromSymbol, toSymbol);
        if (!isSwap)
        {
            return false;
        }

        return
            _supportedTokenSwapOptions.Value.SwapTokenMap.Any(config =>
                config.Key == fromSymbol && config.Value.Any(token =>
                    token.Symbol == toSymbol && token.ChainIdList.Any(chainId => chainId == toChainId)));
    }

    public List<TokenConfigDto> GetTokenListByType(string type, string chainId)
    {
        var result = new List<TokenConfigDto>();
        Dictionary<string, StatusInfo> tokens = null;

        if (type == OrderTypeEnum.Deposit.ToString())
        {
            tokens = _supportedChainTokensOptions.Value.Tokens[chainId].Deposit;
        }
        else if (type == OrderTypeEnum.Withdraw.ToString())
        {
            tokens = _supportedChainTokensOptions.Value.Tokens[chainId].Withdraw;
        }
        else
        {
            tokens = _supportedChainTokensOptions.Value.Transfer;
        }

        foreach (var pair in tokens)
        {
            var symbol = pair.Key;
            chainId ??= ChainId.AELF; // Default to AELF if chainId is null
            if (!_tokenInfoOptions.Value.Tokens.TryGetValue(chainId, out var tokenInfoDic))
                continue;
            if (!tokenInfoDic.TryGetValue(symbol, out var tokenInfo))
                continue;

            var tokenDto = new TokenConfigDto
            {
                Symbol = tokenInfo.Symbol,
                Name = tokenInfo.Name,
                Decimals = tokenInfo.Decimal,
                Icon = tokenInfo.Icon,
                ContractAddress = tokenInfo.TokenAddress
            };

            result.Add(tokenDto);
        }

        return result;
    }

    public bool IsDepositHealth(string symbol, string chainId)
    {
        if (!_supportedChainTokensOptions.Value.Tokens.TryGetValue(chainId, out var chainTokens))
        {
            return false;
        }

        return chainTokens.Deposit.TryGetValue(symbol, out var statusInfo) && statusInfo.IsOpen;
    }

    public bool IsWithdrawHealth(string symbol, string chainId)
    {
        if (!_supportedChainTokensOptions.Value.Tokens.TryGetValue(chainId, out var chainTokens))
        {
            return false;
        }

        return chainTokens.Withdraw.TryGetValue(symbol, out var statusInfo) && statusInfo.IsOpen;
    }
}