using System.Threading.Tasks;

namespace ETransferServer.Common.ChainsClient;

public interface IBlockchainClientProviderFactory
{
    Task<IBlockchainClientProvider> GetBlockChainClientProviderAsync(string chainId);
}