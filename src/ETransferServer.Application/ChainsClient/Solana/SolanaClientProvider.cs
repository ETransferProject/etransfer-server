using System;
using System.Net.Http;
using System.Threading.Tasks;
using ETransferServer.ChainsClient.Tron;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using Microsoft.Extensions.Logging;
using Solnet.Rpc;

namespace ETransferServer.ChainsClient.Solana;

public class SolanaClientProvider : IBlockchainClientProvider
{
    protected readonly IBlockchainClientFactory<IRpcClient> BlockchainClientFactory;
    private readonly ILogger<SolanaClientProvider> _logger;

    public SolanaClientProvider(
        ILogger<SolanaClientProvider> logger, IBlockchainClientFactory<IRpcClient> blockchainClientFactory)
    {
        _logger = logger;
        BlockchainClientFactory = blockchainClientFactory;
    }

    public BlockchainType ChainType { get; } = BlockchainType.Solana;

    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        AssertHelper.NotNull(txId,"TxId can not be null.");
        var result = new BlockDtos();
        var client = BlockchainClientFactory.GetClient(chainId);
        var response = await client.GetTransactionAsync(txId);
        AssertHelper.IsTrue(response.WasSuccessful,$"Solana client failed with {response.HttpStatusCode.ToString()}.");
        var blockTime = response.Result.BlockTime;
        result.BlockTimeStamp = blockTime ?? 0;
        return result;
    }
}