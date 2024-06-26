using System;
using System.Net.Http;
using System.Threading.Tasks;
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

    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        var url = $"{_blockChainInfoOptions.ChainInfos[chainId].Api}" + BlockById;
        var result = new BlockDtos();
        try
        {
            var res = await _httpProvider.InvokeAsync<TronResponse>(HttpMethod.Post, url,
                body: JsonConvert.SerializeObject(new TronQueryParam
                {
                    Value = blockHash
                }, HttpProvider.DefaultJsonSettings));
            result.BlockTimeStamp = res.BlockHeader.RawData.TimeStamp;
            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get tron transaction info, {blockHash},{txId}", blockHash, txId);
            return result;
        }
    }
}