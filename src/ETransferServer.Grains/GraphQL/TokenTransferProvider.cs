using GraphQL;
using Microsoft.Extensions.Logging;
using ETransferServer.Common;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Samples.GraphQL;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.GraphQL;

public interface ITokenTransferProvider
{
    Task<long> GetIndexBlockHeightAsync(string chainId);
    Task<LatestBlockDto> GetLatestBlockAsync(string chainId);
    Task<PagedResultDto<TransferDto>> GetTokenTransferInfoByTxIdsAsync(List<string> txIds, long endHeight);
    Task<PagedResultDto<TransferDto>> GetTokenTransferInfoByBlockHeightAsync(int startHeight, int endHeight, int pageSize, int skipCount);
}

public class TokenTransferProvider : ITokenTransferProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<TokenTransferProvider> _logger;

    public TokenTransferProvider(IGraphQlHelper graphQlHelper, ILogger<TokenTransferProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }
    
    public async Task<long> GetIndexBlockHeightAsync(string chainId)
    {
        try
        {
            var res = await _graphQlHelper.QueryAsync<GraphQLResponse<SyncStateDto>>(new GraphQLRequest
            {
                Query = @"
			    query($chainId:String,$filterType:BlockFilterType!) {
                    data:syncState(input: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight}
                    }",
                Variables = new
                {
                    chainId,
                    filterType = BlockFilterType.TRANSACTION
                }
            });
            return res.Data.ConfirmedBlockHeight;
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
                    chainId,blockHash, blockHeight, previousBlockHash, blockTime, confirmed
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

    public async Task<PagedResultDto<TransferDto>> GetTokenTransferInfoByBlockHeightAsync(int startHeight, int endHeight, int resultCount, int skipCount)
    {
        return await GetTokenTransferInfoAsync(null, startHeight, endHeight, resultCount, skipCount);
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
                    $txIds:[String],
                    $startBlockHeight:Long!,
                    $endBlockHeight:Long!,
                    $skipCount:Int!,
                    $maxResultCount:Int!
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
                            toAddress,to,params,signature,index,
                            status
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
}