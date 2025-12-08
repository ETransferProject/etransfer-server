using System.Threading.Tasks;

namespace ETransferServer.Common.ChainsClient;

public interface IBlockchainClientProvider
{
    BlockchainType ChainType { get; }
    Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null);
    Task<string> GetMemoAsync(string chainId, string txId);
}