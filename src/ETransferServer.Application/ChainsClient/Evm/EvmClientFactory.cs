using ETransferServer.Common.ChainsClient;
using ETransferServer.Options;
using Microsoft.Extensions.Options;

namespace ETransferServer.ChainsClient.Evm;

public class EvmClientFactory : IBlockchainClientFactory<Nethereum.Web3.Web3>
{
    private readonly BlockChainInfoOptions _blockChainInfoOptions;

    public EvmClientFactory(IOptionsSnapshot<BlockChainInfoOptions> apiOptions)
    {
        _blockChainInfoOptions = apiOptions.Value;
    }

    public Nethereum.Web3.Web3 GetClient(string chainId)
    {
        return new Nethereum.Web3.Web3(_blockChainInfoOptions.ChainInfos[chainId].Api);
    }
}