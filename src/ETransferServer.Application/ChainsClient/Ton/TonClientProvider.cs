using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AElf;
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

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TonClientProvider), 
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<BlockDtos> GetBlockTimeAsync(string chainId, string blockHash, string txId = null)
    {
        _logger.LogInformation("GetBlockTimeAsync ton, chainId:{chanId}, blockHash:{blockHash}, txId:{txId}", 
            chainId, blockHash, txId);
        var result = new BlockDtos();
        var baseUrlList = _blockChainInfoOptions.ChainInfos[chainId].Api.Split(CommonConstant.Comma, StringSplitOptions.TrimEntries).ToList();
        foreach (var baseUrl in baseUrlList)
        {
            try
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
            catch (Exception e)
            {
                _logger.LogError(e, "GetBlockTimeAsync ton error, url:{url}", baseUrl);
            }
        }

        return result;
    }
    
    public async Task<string> GetMemoAsync(string chainId, string txId)
    {
        if (txId.IsNullOrEmpty()) return null;
        _logger.LogInformation("GetMemoAsync ton, txId:{txId}", txId);
        var memo = string.Empty;
        var baseUrlList = _blockChainInfoOptions.ChainInfos[chainId].Api.Split(CommonConstant.Comma, StringSplitOptions.TrimEntries).ToList();
        foreach (var baseUrl in baseUrlList)
        {
            try
            {
                var (tonType, url, param) = ApiHelper.GetApiInfo(baseUrl, txId);
                switch (tonType)
                {
                    case TonType.TonApi:
                        var respApi = await _httpProvider.InvokeAsync<TonApiMemoResponse>(HttpMethod.Get,
                            url, param: param);
                        AssertHelper.NotNull(respApi, "Empty tonApi response");
                        AssertHelper.NotEmpty(respApi.OutMsgs, "Empty tonApi outMsgs");
                        _logger.LogInformation("GetMemoAsync ton, sum_type:{sumType}, value:{value}", 
                            respApi.OutMsgs[0].DecodedBody.ForwardPayload.Value.SumType,
                            respApi.OutMsgs[0].DecodedBody.ForwardPayload.Value.Value);
                        var memoHex = respApi.OutMsgs[0].DecodedBody.ForwardPayload.Value.Value;
                        var memoRaw = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(memoHex));
                        AssertHelper.IsTrue(memoRaw.Length >= 36, "invalid tonApi memo length");
                        memo = memoRaw[^36..];
                        break;
                    default:
                        throw new NotSupportedException();
                }

                AssertHelper.IsTrue(!memo.IsNullOrEmpty(), "GetMemoAsync ton, memo:{memo}", memo);
                return memo;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetMemoAsync ton error, url:{url}", baseUrl);
            }
        }

        return memo;
    }
    
    public async Task<FlowBehavior> HandleExceptionAsync(Exception ex, string chainId, string blockHash, string txId)
    {
        _logger.LogError(ex, "Failed to get ton transaction info, {blockHash},{txId}", blockHash, txId);
        return new FlowBehavior
        {
            ExceptionHandlingStrategy = ExceptionHandlingStrategy.Return,
            ReturnValue = new BlockDtos()
        };
    }
}