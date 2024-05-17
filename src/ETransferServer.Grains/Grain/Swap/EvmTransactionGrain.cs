using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Swap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace ETransferServer.Grains.Grain.Swap;

public interface IEvmTransactionGrain : IGrainWithStringKey
{
    Task<GrainResultDto<long>> GetTransactionTimeAsync(string chainId, string blockHash, string txId, int retryTime = 0);
}

public class EvmTransactionGrain : Grain<EvmTransactionState>, IEvmTransactionGrain
{
    private readonly IBlockchainClientProviderFactory _blockchainClientProvider;
    private readonly ILogger<EvmTransactionGrain> _logger;
    private readonly SwapInfosOptions _swapInfosOptions;

    public EvmTransactionGrain(IBlockchainClientProviderFactory blockchainClientProvider, ILogger<EvmTransactionGrain> logger,
        IOptionsSnapshot<SwapInfosOptions> swapInfosOptions)
    {
        _blockchainClientProvider = blockchainClientProvider;
        _logger = logger;
        _swapInfosOptions = swapInfosOptions.Value;
    }

    public async Task<GrainResultDto<long>> GetTransactionTimeAsync(string chainId, string blockHash, string txId,
        int retryTime = 0)
    {
        if (retryTime > _swapInfosOptions.CallTxRetryTimes)
        {
            _logger.LogError("Get {chainId} transaction time failed after retry {times}.{grainId}",
                chainId, _swapInfosOptions.CallTxRetryTimes,
                this.GetPrimaryKeyString());
            return new GrainResultDto<long>
            {
                Success = false,
                Message = "Get transaction time failed after retry"
            };
        }

        if (State.BlockTime > 0)
        {
            return new GrainResultDto<long>
            {
                Data = State.BlockTime
            };
        }

        try
        {
            var provider = await _blockchainClientProvider.GetBlockChainClientProviderAsync(chainId);
            BlockDtos block;
            long time;
            switch (provider.ChainType)
            {
                case BlockchainType.Tron:
                    block = await provider.GetBlockTimeAsync(chainId, blockHash);
                    AssertHelper.IsTrue(long.TryParse(block.BlockTimeStamp.ToString(), out time));
                    break;
                case BlockchainType.Solana:
                    block = await provider.GetBlockTimeAsync(chainId, blockHash, txId);
                    AssertHelper.IsTrue(long.TryParse(block.BlockTimeStamp.ToString(), out time));
                    time *= 1000;
                    break;
                case BlockchainType.Evm:
                {
                    block = await provider.GetBlockTimeAsync(chainId, blockHash);
                    if (!block.TransactionIdList.Contains(txId))
                    {
                        _logger.LogError("Block {blockHash} not contains transaction {txId}.{grainId}", blockHash,
                            chainId,
                            this.GetPrimaryKeyString());
                        return new GrainResultDto<long>
                        {
                            Success = false,
                            Message = "Block not contains transaction"
                        };
                    }
                    AssertHelper.IsTrue(long.TryParse(block.BlockTimeStamp.ToString(), out time));
                    time *= 1000;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            State.TxId = txId;
            State.BlockTime = time;
            await WriteStateAsync();
            return new GrainResultDto<long>
            {
                Success = true,
                Data = time
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get {chainId} transaction time.{grainId}", chainId,
                this.GetPrimaryKeyString());
            retryTime += 1;
            await GetTransactionTimeAsync(chainId, blockHash, txId, retryTime);
        }

        return new GrainResultDto<long>
        {
            Success = false,
            Message = "Get transaction time failed."
        };
    }
}