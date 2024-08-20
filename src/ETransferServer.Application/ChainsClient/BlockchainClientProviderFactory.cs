using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ChainsClient;

public class BlockchainClientProviderFactory : IBlockchainClientProviderFactory,ITransientDependency
{
    private readonly IEnumerable<IBlockchainClientProvider> _blockchainClientProviders;
    private readonly BlockChainInfoOptions _blockChainInfoOptions;

    public BlockchainClientProviderFactory(IEnumerable<IBlockchainClientProvider> blockchainClientProviders,
        IOptionsSnapshot<BlockChainInfoOptions> blockChainInfoOptions)
    {
        _blockchainClientProviders = blockchainClientProviders;
        _blockChainInfoOptions = blockChainInfoOptions.Value;
    }

    public async Task<IBlockchainClientProvider> GetBlockChainClientProviderAsync(string chainId)
    {
        var chain = _blockChainInfoOptions.ChainInfos[chainId];
        return chain == null ? null : _blockchainClientProviders.First(o => o.ChainType == chain.ChainType);
    }
}