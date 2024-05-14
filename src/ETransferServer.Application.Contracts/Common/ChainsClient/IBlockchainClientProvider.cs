using System.Threading.Tasks;

namespace ETransferServer.Common.ChainsClient;

public interface IBlockchainClientProvider
{
    BlockchainType ChainType { get; }
    Task<BlockDtos> GetBlocksAsync(string chainId, string blockHash);
}