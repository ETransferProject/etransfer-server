using System;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.ChainsClient.Evm;

public class EvmClientProvider : IBlockchainClientProvider
{
    protected readonly IBlockchainClientFactory<Nethereum.Web3.Web3> BlockchainClientFactory;
    private readonly BlockChainInfoOptions _blockChainInfoOptions;
    private readonly ILogger<EvmClientProvider> _logger;

    public EvmClientProvider(IBlockchainClientFactory<Nethereum.Web3.Web3> blockchainClientFactory, 
        IOptionsSnapshot<BlockChainInfoOptions> apiOptions,
        ILogger<EvmClientProvider> logger)
    {
        BlockchainClientFactory = blockchainClientFactory;
        _blockChainInfoOptions = apiOptions.Value;
        _logger = logger;
    }

    public BlockchainType ChainType  { get; } = BlockchainType.Evm;
    
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash,string txId = null)
    {
        var client = BlockchainClientFactory.GetClient(chainId);
        var timeout = TimeSpan.FromSeconds(_blockChainInfoOptions.TimeOut);
        var timeoutTask = Task.Delay(timeout);

        _logger.LogInformation("Evm start, timeOut:{timeOut}", _blockChainInfoOptions.TimeOut);
        var blockTask = client.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(blockHash);
        
        var completedTask = await Task.WhenAny(blockTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            _logger.LogInformation("Evm cancel");
            return new BlockDtos();
        }
        
        var block = await blockTask;
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