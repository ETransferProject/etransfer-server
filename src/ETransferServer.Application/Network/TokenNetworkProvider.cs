using System;
using System.Collections.Generic;
using System.Linq;
using ETransferServer.Common;
using ETransferServer.Options;
using Microsoft.Extensions.Options;

namespace ETransferServer.Network;

public interface ITokenNetworkProvider
{
    List<string> GetSupportTransferSymbolList();
    List<NetworkBasicInfo> GetSupportedNetworkInfoList(string symbol);
    NetworkBasicInfo GetNetworkInfo(string network);
    List<string> GetExtraNotesTemplate();
    List<string> GetSwapExtraNotesTemplate();
}

public class TokenNetworkProvider : ITokenNetworkProvider
{
    private readonly IOptionsSnapshot<TokenSupportedChainInfoOptions> _tokenSupportedChainOptions;
    private readonly IOptionsSnapshot<NetworkInfoOptions> _networkInfoOptions;
    private readonly IOptionsSnapshot<SupportedChainTokensOptions> _supportedChainTokensOptions;

    public TokenNetworkProvider(IOptionsSnapshot<TokenSupportedChainInfoOptions> tokenSupportedChainOptions, IOptionsSnapshot<NetworkInfoOptions> networkInfoOptions, IOptionsSnapshot<SupportedChainTokensOptions> supportedChainTokensOptions)
    {
        _tokenSupportedChainOptions = tokenSupportedChainOptions;
        _networkInfoOptions = networkInfoOptions;
        _supportedChainTokensOptions = supportedChainTokensOptions;
    }

    public List<string> GetSupportTransferSymbolList()
    {
        return _supportedChainTokensOptions.Value.Transfer.Keys.ToList();
    }

    public List<NetworkBasicInfo> GetSupportedNetworkInfoList(string symbol)
    {
        if (!_tokenSupportedChainOptions.Value.SupportedChains.TryGetValue(symbol, out var supportedChains))
        {
            return new List<NetworkBasicInfo>();
        }

        var networkInfos = new List<NetworkBasicInfo>();
        foreach (var chain in supportedChains)
        {
            if (!_networkInfoOptions.Value.Networks.TryGetValue(chain.Network, out var networkInfo))
            {
                continue;
            }

            networkInfos.Add(networkInfo);
        }

        return networkInfos;
    }

    public NetworkBasicInfo GetNetworkInfo(string network)
    {
        return !_networkInfoOptions.Value.Networks.TryGetValue(network, out var networkInfo) ? null : networkInfo;
    }

    public List<string> GetExtraNotesTemplate()
    {
        return _networkInfoOptions.Value.ExtraNotesTemplate;
    }

    public List<string> GetSwapExtraNotesTemplate()
    {
        return _networkInfoOptions.Value.SwapExtraNotesTemplate;
    }
}