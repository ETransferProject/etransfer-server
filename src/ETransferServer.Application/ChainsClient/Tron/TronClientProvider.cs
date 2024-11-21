using System;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Common.HttpClient;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ETransferServer.ChainsClient.Tron;

public class TronClientProvider : IBlockchainClientProvider
{
    private readonly IHttpProvider _httpProvider;
    private readonly ILogger<TronClientProvider> _logger;
    private readonly BlockChainInfoOptions _blockChainInfoOptions;
    private const string BlockById = "/wallet/getblockbyid";


    public TronClientProvider(
        ILogger<TronClientProvider> logger, IHttpProvider httpProvider,
        IOptionsSnapshot<BlockChainInfoOptions> blockChainInfoOptions)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _blockChainInfoOptions = blockChainInfoOptions.Value;
    }

    public BlockchainType ChainType { get; } = BlockchainType.Tron;

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TronClientProvider), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        var url = $"{_blockChainInfoOptions.ChainInfos[chainId].Api}" + BlockById;
        var result = new BlockDtos();
        var res = await _httpProvider.InvokeAsync<TronResponse>(HttpMethod.Post, url,
            body: JsonConvert.SerializeObject(new TronQueryParam
            {
                Value = blockHash
            }, HttpProvider.DefaultJsonSettings));
        result.BlockTimeStamp = res.BlockHeader.RawData.TimeStamp;
        return result;
    }
    
    public async Task<string> GetMemoAsync(string chainId, string txId)
    {
        return null;
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, string chainId, string blockHash, string txId)
    {
        _logger.LogError(ex, "Failed to get tron transaction info, {blockHash},{txId}", blockHash, txId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new BlockDtos()
        };
    }
}