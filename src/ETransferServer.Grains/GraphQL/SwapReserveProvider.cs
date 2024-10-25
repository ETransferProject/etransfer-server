using AElf.ExceptionHandler;
using AElf.OpenTelemetry.ExecutionTime;
using ETransferServer.Common.GraphQL;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.GraphQL;
using ETransferServer.Options;
using ETransferServer.Samples.HttpClient;
using GraphQL;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.GraphQL;

public interface ISwapReserveProvider
{
    Task<PagedResultDto<ReserveDto>> GetReserveAsync(string chainId, string pairAddress, long timestamp, int skipCount,
        int maxResultCount);

    Task<long> GetConfirmedHeightAsync(string chainId);
}

[AggregateExecutionTime]
public class SwapReserveProvider : ISwapReserveProvider, ISingletonDependency
{
    private ApiInfo _swapSyncStateUri => new (HttpMethod.Get, _syncStateServiceOption.Value.SwapSyncStateUri);
    
    private readonly IGraphQLClientFactory _graphQlClientFactory;
    private readonly HttpProvider _httpProvider;
    private readonly IOptionsSnapshot<SyncStateServiceOption> _syncStateServiceOption;
    private readonly ILogger<SwapReserveProvider> _logger;

    public SwapReserveProvider(IGraphQLClientFactory graphQlClientFactory,
        HttpProvider httpProvider,
        IOptionsSnapshot<SyncStateServiceOption> syncStateServiceOption,
        ILogger<SwapReserveProvider> logger)
    {
        _graphQlClientFactory = graphQlClientFactory;
        _httpProvider = httpProvider;
        _syncStateServiceOption = syncStateServiceOption;
        _logger = logger;
    }

    [ExceptionHandler(typeof(Exception), LogLevel = LogLevel.Error, 
        Message = "Query token transfer error", ReturnDefault = ReturnDefault.New)]
    public async Task<PagedResultDto<ReserveDto>> GetReserveAsync(string chainId, string pairAddress, long timestamp,
        int skipCount,
        int maxResultCount)
    {
        _logger.LogInformation("Query from gql:{chainId},{pairAddress},{timestamp}",chainId,pairAddress,timestamp);
        var res = await _graphQlClientFactory.GetClient(GraphQLClientEnum.SwapClient)
            .SendQueryAsync<GraphQLResponse<PagedResultDto<ReserveDto>>>(new GraphQLRequest
        {
            Query = @"
			query(
                $chainId:String,
                $pairAddress:String,
                $timestampMax:Long
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

    [ExceptionHandler(typeof(Exception), LogLevel = LogLevel.Error, 
        Message = "Query swap syncState error", ReturnDefault = ReturnDefault.Default)]
    public async Task<long> GetConfirmedHeightAsync(string chainId)
    {
        var res = await _httpProvider.InvokeAsync<SyncStateResponse>(_syncStateServiceOption.Value.BaseUrl, _swapSyncStateUri);
        return res.CurrentVersion.Items.FirstOrDefault(i => i.ChainId == chainId)?.LastIrreversibleBlockHeight ?? 0;
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