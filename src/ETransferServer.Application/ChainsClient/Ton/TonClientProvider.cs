using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using ETransferServer.ChainsClient.Ton.Helper;
using ETransferServer.Common;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Common.HttpClient;
using ETransferServer.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETransferServer.ChainsClient.Ton;

public class TonClientProvider : IBlockchainClientProvider
{
    private readonly IHttpProvider _httpProvider;
    private readonly ILogger<TonClientProvider> _logger;
    private readonly BlockChainInfoOptions _blockChainInfoOptions;
    
    public TonClientProvider(
        ILogger<TonClientProvider> logger, IHttpProvider httpProvider,
        IOptionsSnapshot<BlockChainInfoOptions> blockChainInfoOptions)
    {
        _logger = logger;
        _httpProvider = httpProvider;
        _blockChainInfoOptions = blockChainInfoOptions.Value;
    }

    public BlockchainType ChainType { get; } = BlockchainType.Ton;

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHelper),
        MethodName = nameof(ExceptionHelper.HandleException))]
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        _logger.LogInformation("GetBlockTimeAsync ton, chainId:{chanId}, blockHash:{blockHash}, txId:{txId}", 
            chainId, blockHash, txId);
        var result = new BlockDtos();
       
        var baseUrlList = _blockChainInfoOptions.ChainInfos[chainId].Api.Split(CommonConstant.Comma, StringSplitOptions.TrimEntries).ToList();
        foreach (var baseUrl in baseUrlList)
        {
            var (tonType, url, param) = ApiHelper.GetApiInfo(baseUrl, txId);
            switch (tonType)
            {
                case TonType.TonApi:
                    var respApi = await _httpProvider.InvokeAsync<TonApiResponse>(HttpMethod.Get,
                        url, param: param);
                    AssertHelper.NotNull(respApi, "Empty tonApi result");
                    result.BlockTimeStamp = respApi.Utime;
                    break;
                case TonType.TonCenter:
                    var respCenter = await _httpProvider.InvokeAsync<TonCenterResponse>(HttpMethod.Get,
                        url, param: param);
                    AssertHelper.NotNull(respCenter, "Empty tonCenter result");
                    var txs = respCenter.Transactions;
                    if (!txs.IsNullOrEmpty() && txs.Count > 0)
                    {
                        result.BlockTimeStamp = txs[0].Now;
                    }
                    break;
                default:
                    throw new NotSupportedException();
            }

            AssertHelper.IsTrue(
                result.BlockTimeStamp != null &&
                long.TryParse(result.BlockTimeStamp.ToString(), out var time) && time > 0,
                "GetBlockTimeAsync BlockTimeStamp, time:{time}", result.BlockTimeStamp);
            return result;
        }
            
        return result;
    }
}