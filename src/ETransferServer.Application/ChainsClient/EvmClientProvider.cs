using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;

namespace ETransferServer.ChainsClient;

public class EvmClientProvider : IBlockchainClientProvider
{
    protected readonly IBlockchainClientFactory<Nethereum.Web3.Web3> BlockchainClientFactory;

    public EvmClientProvider(IBlockchainClientFactory<Nethereum.Web3.Web3> blockchainClientFactory)
    {
        BlockchainClientFactory = blockchainClientFactory;
    }

    public BlockchainType ChainType  { get; } = BlockchainType.Evm;
    
    public async Task<BlockDtos> GetBlocksAsync(string chainId, string blockHash)
    {
        var client = BlockchainClientFactory.GetClient(chainId);
        var block = await client.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockHash);
        return new BlockDtos
        {
            BlockHash = block.BlockHash,
            BlockTimeStamp = block.Timestamp.Value,
            BlockHeight = block.Number.Value,
            TransactionIdList = block.Transactions.ToList().Select(t => t.TransactionHash).ToList()
        };
    }
}