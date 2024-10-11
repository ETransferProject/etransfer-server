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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHelper),
        MethodName = nameof(ExceptionHelper.HandleException))]
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
}