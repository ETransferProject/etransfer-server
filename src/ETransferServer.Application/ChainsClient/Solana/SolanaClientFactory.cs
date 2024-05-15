using ETransferServer.Common.ChainsClient;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
using Solnet.Rpc;

namespace ETransferServer.ChainsClient.Solana;

public class SolanaClientFactory : IBlockchainClientFactory<IRpcClient>
{
    private readonly BlockChainInfoOptions _blockChainInfoOptions;

    public SolanaClientFactory(IOptionsSnapshot<BlockChainInfoOptions> apiOptions)
    {
        _blockChainInfoOptions = apiOptions.Value;
    }

    public IRpcClient GetClient(string chainId)
    {
        var rpcClient = ClientFactory.GetClient(_blockChainInfoOptions.ChainInfos[chainId].Api);
        return rpcClient;
    }
}