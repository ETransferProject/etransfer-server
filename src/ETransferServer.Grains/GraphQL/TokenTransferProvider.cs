using AElf.OpenTelemetry.ExecutionTime;
using GraphQL;
using Microsoft.Extensions.Logging;
using ETransferServer.Common;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Options;
using ETransferServer.Samples.GraphQL;
using ETransferServer.Samples.HttpClient;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.GraphQL;

public interface ITokenTransferProvider
{
    Task<long> GetIndexBlockHeightAsync(string chainId);
    Task<LatestBlockDto> GetLatestBlockAsync(string chainId);
    Task<PagedResultDto<TransferDto>> GetTokenTransferInfoByTxIdsAsync(List<string> txIds, long endHeight);
    Task<PagedResultDto<SwapRecordDto>> GetSwapTokenInfoByTxIdsAsync(List<string> txIds, long endHeight);
    Task<PagedResultDto<TransferRecordDto>> GetTokenPoolRecordListAsync(long timestampMin, long timestampMax,
        int maxResultCount, int skipCount);
}

[AggregateExecutionTime]
public class TokenTransferProvider : ITokenTransferProvider, ISingletonDependency
{
    private ApiInfo _syncStateUri => new (HttpMethod.Get, _syncStateServiceOption.Value.SyncStateUri);

    private readonly IGraphQlHelper _graphQlHelper;
    private readonly HttpProvider _httpProvider;
    private readonly IOptionsSnapshot<SyncStateServiceOption> _syncStateServiceOption;
    private readonly ILogger<TokenTransferProvider> _logger;

    public TokenTransferProvider(IGraphQlHelper graphQlHelper, 
        HttpProvider httpProvider,
        IOptionsSnapshot<SyncStateServiceOption> syncStateServiceOption,
        ILogger<TokenTransferProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _httpProvider = httpProvider;
        _syncStateServiceOption = syncStateServiceOption;
        _logger = logger;
    }
    
    public async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        try
        {
            var res = await _httpProvider.InvokeAsync<SyncStateResponse>(_syncStateServiceOption.Value.BaseUrl, _syncStateUri);
            return res.CurrentVersion.Items.FirstOrDefault(i => i.ChainId == chainId)?.LastIrreversibleBlockHeight ?? 0;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query syncState error");
            return 0;
        }
    }

    public async Task<LatestBlockDto> GetLatestBlockAsync(string chainId)
    {
        try
        {
            var resp = await _graphQlHelper.QueryAsync<GraphQLResponse<LatestBlockDto>>(new GraphQLRequest
            {
                Query = @"query(
                $chainId:String!        
            ) {
                data:getLatestBlock (
                    input: {
                        chainId: $chainId
                    }
                ){
                    chainId, blockHash, blockHeight, blockTime, confirmed
                }
            }",
                Variables = new
                {
                    chainId = chainId
                }

            });
            AssertHelper.NotNull(resp, "Empty result");
            AssertHelper.NotNull(resp.Data, "Empty result data");
            return resp.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query latestBlock error");
            return null;
        }
    }

    public async Task<PagedResultDto<TransferDto>> GetTokenTransferInfoByTxIdsAsync(List<string> txIds, long endHeight)
    {
        if (txIds.IsNullOrEmpty())
            return new PagedResultDto<TransferDto>();
        
        return await GetTokenTransferInfoAsync(txIds, 0, endHeight, txIds.Count, 0);
    }

    public async Task<PagedResultDto<SwapRecordDto>> GetSwapTokenInfoByTxIdsAsync(List<string> txIds, long endHeight)
    {
        if (txIds.IsNullOrEmpty())
            return new PagedResultDto<SwapRecordDto>();
        
        return await GetSwapTokenInfoAsync(txIds, 0, endHeight, txIds.Count, 0);
    }

    public async Task<PagedResultDto<TransferRecordDto>> GetTokenPoolRecordListAsync(long timestampMin, long timestampMax, 
        int maxResultCount, int skipCount = 0)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<GraphQLResponse<PagedResultDto<TransferRecordDto>>>(new GraphQLRequest
            {
                Query = @"
			    query(
                    $timestampMin:Long!,
                    $timestampMax:Long!,
                    $startBlockHeight:Long!,
                    $endBlockHeight:Long!,
                    $skipCount:Int,
                    $maxResultCount:Int,
                    $isFilterEmpty: Boolean!,
                    $transferType: TokenTransferType!
                ) {
                    data:getTokenPoolRecords(
                        input: {
                            timestampMin:$timestampMin,
                            timestampMax:$timestampMax,
                            startBlockHeight:$startBlockHeight,
                            endBlockHeight:$endBlockHeight,
                            skipCount:$skipCount,
                            maxResultCount:$maxResultCount,
                            isFilterEmpty:$isFilterEmpty,
                            transferType:$transferType
                        }
                    ){
                        items:data{
                            id,transactionId,methodName,from,to,
                            toChainId,toAddress,symbol,amount,maxEstimateFee,
                            timestamp,transferType,chainId,blockHash,blockHeight,memo
                        },
                        totalCount
                    }
                }",
                Variables = new
                {
                    timestampMin = timestampMin,
                    timestampMax = timestampMax,
                    startBlockHeight = 0,
                    endBlockHeight = 0,
                    skipCount = skipCount,
                    maxResultCount = maxResultCount,
                    isFilterEmpty = true,
                    transferType = TokenTransferType.In
                }
            });
            return res.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query token pool records error");
            return new PagedResultDto<TransferRecordDto>();
        }
    }


    private async Task<PagedResultDto<TransferDto>> GetTokenTransferInfoAsync(List<string> txIds, long startBlockHeight,
        long endBlockHeight, int inputMaxResultCount, int inputSkipCount = 0)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<GraphQLResponse<PagedResultDto<TransferDto>>>(new GraphQLRequest
            {
                Query = @"
			    query(
                    $txIds:[String!]!,
                    $startBlockHeight:Long!,
                    $endBlockHeight:Long!,
                    $skipCount:Int,
                    $maxResultCount:Int
                ) {
                    data:getTransaction(
                        input: {
                            startBlockHeight:$startBlockHeight,
                            endBlockHeight:$endBlockHeight,
                            transactionIds:$txIds,
                            skipCount:$skipCount,
                            maxResultCount:$maxResultCount
                        }
                    ){
                        items:data{
                            id,transactionId,methodName,timestamp,chainId,
                            blockHash,blockHeight,amount,symbol,fromAddress,from,
                            toAddress,to,params,index,status
                        },
                        totalCount
                    }
                }",
                Variables = new
                {
                    txIds = txIds,
                    skipCount = inputSkipCount,
                    maxResultCount = inputMaxResultCount,
                    startBlockHeight = startBlockHeight,
                    endBlockHeight = endBlockHeight
                }
            });
            return res.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query token transfer error");
            return new PagedResultDto<TransferDto>();
        }

    }
    
    private async Task<PagedResultDto<SwapRecordDto>> GetSwapTokenInfoAsync(List<string> txIds, long startBlockHeight,
        long endBlockHeight, int inputMaxResultCount, int inputSkipCount = 0)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<GraphQLResponse<PagedResultDto<SwapRecordDto>>>(new GraphQLRequest
            {
                Query = @"
			    query(
                    $txIds:[String!]!,
                    $startBlockHeight:Long,
                    $endBlockHeight:Long,
                    $skipCount:Int,
                    $maxResultCount:Int
                ) {
                    data:getSwapTokenRecord(
                        input: {
                            startBlockHeight:$startBlockHeight,
                            endBlockHeight:$endBlockHeight,
                            transactionIds:$txIds,
                            skipCount:$skipCount,
                            maxResultCount:$maxResultCount
                        }
                    ){
                        items:data{
                            transactionId,symbolIn,symbolOut,amountIn,amountOut,
                            fromAddress,toAddress,channel,feeRate,blockHeight
                        },
                        totalCount
                    }
                }",
                Variables = new
                {
                    txIds = txIds,
                    skipCount = inputSkipCount,
                    maxResultCount = inputMaxResultCount,
                    startBlockHeight = startBlockHeight,
                    endBlockHeight = endBlockHeight
                }
            });
            return res.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query swap token error");
            return new PagedResultDto<SwapRecordDto>();
        }

    }
}

public enum TokenTransferType
{
    All,
    In,
    Out
}