using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using Microsoft.Extensions.Logging;

namespace ETransferServer.ChainsClient.Evm;

public class EvmClientProvider : IBlockchainClientProvider
{
    protected readonly IBlockchainClientFactory<Nethereum.Web3.Web3> BlockchainClientFactory;
    private readonly ILogger<EvmClientProvider> _logger;

    public EvmClientProvider(IBlockchainClientFactory<Nethereum.Web3.Web3> blockchainClientFactory, ILogger<EvmClientProvider> logger)
    {
        BlockchainClientFactory = blockchainClientFactory;
        _logger = logger;
    }

    public BlockchainType ChainType  { get; } = BlockchainType.Evm;
    
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash,string txId = null)
    {
        var client = BlockchainClientFactory.GetClient(chainId);
        var block = await client.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockHash);
        _logger.LogInformation("Get time from block.{chainId},{blockHash},{time}",chainId,block.BlockHash,block.Timestamp.Value);
        return new BlockDtos
        {
            BlockHash = block.BlockHash,
            BlockTimeStamp = block.Timestamp.Value,
            BlockHeight = block.Number.Value,
            TransactionIdList = block.Transactions.ToList().Select(t => t.TransactionHash).ToList()
        };
    }
}