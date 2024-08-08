using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        _logger.LogInformation("GetBlockTimeAsync ton, chainId:{chanId}, blockHash:{blockHash}, txId:{txId}", 
            chainId, blockHash, txId);
        var result = new BlockDtos();
        try
        {
            var baseUrlList = _blockChainInfoOptions.ChainInfos[chainId].Api.Split(CommonConstant.Comma, StringSplitOptions.TrimEntries).ToList();
            foreach (var baseUrl in baseUrlList)
            {
                try
                {
                    var (tonType, url, param) = ApiHelper.GetApiInfo(baseUrl, txId);
                    switch (tonType)
                    {
                        case TonType.TonScan:
                            var respScan = await _httpProvider.InvokeAsync<TonScanResponse>(HttpMethod.Get,
                                url, param: param);
                            AssertHelper.NotNull(respScan, "Empty tonScan result");
                            var tx = respScan.Json?.Data?.Transactions;
                            if (!tx.IsNullOrEmpty() && tx.ContainsKey(blockHash))
                            {
                                result.BlockTimeStamp = tx[blockHash].Utime;
                            }
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
                        case TonType.TonApi:
                            var respApi = await _httpProvider.InvokeAsync<TonApiResponse>(HttpMethod.Get,
                                url, param: param);
                            AssertHelper.NotNull(respApi, "Empty tonApi result");
                            result.BlockTimeStamp = respApi.Utime;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                    return result;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "GetBlockTimeAsync ton error, url:{url}", baseUrl);
                }
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to get ton transaction info, {blockHash},{txId}", blockHash, txId);
            return result;
        }
    }
}