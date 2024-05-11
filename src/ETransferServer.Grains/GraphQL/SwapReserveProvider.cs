using ETransferServer.Common.GraphQL;
using ETransferServer.Dtos.GraphQL;
using GraphQL;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.GraphQL;

public interface ISwapReserveProvider
{
    Task<PagedResultDto<ReserveDto>> GetReserveAsync(string chainId, string pairAddress, long? timestamp, int skipCount,
        int maxResultCount);

    Task<long> GetConfirmedHeightAsync(string chainId);
}

public class SwapReserveProvider : ISwapReserveProvider, ISingletonDependency
{
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly ILogger<SwapReserveProvider> _logger;

    public SwapReserveProvider(ILogger<SwapReserveProvider> logger, IGraphQLClientFactory graphQlClientFactory)
    {
        _logger = logger;
        _graphQlClientFactory = graphQlClientFactory;
    }

    public async Task<PagedResultDto<ReserveDto>> GetReserveAsync(string chainId, string pairAddress, long? timestamp,
        int skipCount,
        int maxResultCount)
    {
        try
        {
            var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SwapClient)
                .SendQueryAsync<GraphQLResponse<PagedResultDto<ReserveDto>>>(new GraphQLRequest
            {
                Query = @"
			    query(
                    $chainId:String,
                    $pairAddress:String,
                    $timestampMax:Long!
                    $skipCount:Int!,
                    $maxResultCount:Int!
                ) {
                    data:syncRecord(
                        dto: {
                            chainId:$chainId,
                            pairAddress:$pairAddress,
                            timestampMax:$timestampMax,
                            skipCount:$skipCount,
                            maxResultCount:$maxResultCount
                        }
                    ){
                        items:data{
                            chainId,pairAddress,symbolA,symbolB,reserveA,reserveB,timestamp,blockHeight
                        },
                        totalCount
                    }
                }",
                Variables = new
                {
                    chainId = chainId,
                    pairAddress = pairAddress,
                    timestampMax = timestamp,
                    skipCount = skipCount,
                    maxResultCount = maxResultCount,
                }
            });
            return res.Data.Data;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Query token transfer error");
            return new PagedResultDto<ReserveDto>();
        }
    }

    public async Task<long> GetConfirmedHeightAsync(string chainId)
    {
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SwapClient)
            .SendQueryAsync<ConfirmedBlockHeight>(new GraphQLRequest
        {
            Query = @"
			    query($chainId:String,$filterType:BlockFilterType!) {
                    syncState(dto: {chainId:$chainId,filterType:$filterType}){
                        confirmedBlockHeight}
                    }",
            Variables = new
            {
                chainId,
                filterType = BlockFilterType.LOG_EVENT
            }
        });

        return res.Data.SyncState.ConfirmedBlockHeight;
    }
}

public class ConfirmedBlockHeight
{
    public SyncState SyncState { get; set; }
}

public class SyncState
{
    public long ConfirmedBlockHeight { get; set; }
}

public enum BlockFilterType
{
    BLOCK,
    TRANSACTION,
    LOG_EVENT
}