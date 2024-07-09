using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Orders;
using Microsoft.Extensions.Logging;
using ETransferServer.Service.Info;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Info;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class InfoAppService : ETransferServerAppService, IInfoAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly INetworkAppService _networkAppService;
    private readonly IOptionsSnapshot<NetworkOptions> _networkOptions;
    private readonly IOptionsSnapshot<TokenOptions> _tokenOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<InfoAppService> _logger;

    public InfoAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        INetworkAppService networkAppService, 
        IOptionsSnapshot<NetworkOptions> networkOptions,
        IOptionsSnapshot<TokenOptions> tokenOptions,
        IObjectMapper objectMapper,
        ILogger<InfoAppService> logger)
    {
        _orderIndexRepository = orderIndexRepository;
        _networkAppService = networkAppService;
        _networkOptions = networkOptions;
        _tokenOptions = tokenOptions;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<GetTransactionOverviewResult> GetTransactionOverviewAsync(GetOverviewRequestDto request)
    {
        var result = new GetTransactionOverviewResult();
        try
        {
            result.Transaction.Latest = DateTime.UtcNow.Date.ToUtcString(TimeHelper.DatePattern);
            result.Transaction.TotalTx = await GetOrderCountAsync();

            var dateDimension = (DateDimensionEnum)Enum.Parse(typeof(DateDimensionEnum), request.Type.ToString());
            switch (dateDimension)
            {
                case DateDimensionEnum.Day:
                    result = await QueryCountAggAsync(DateInterval.Day, request.MaxResultCount, result);
                    break;
                case DateDimensionEnum.Week:
                    result = await QueryCountAggAsync(DateInterval.Week, request.MaxResultCount, result);
                    break;
                case DateDimensionEnum.Month:
                    result = await QueryCountAggAsync(DateInterval.Month, request.MaxResultCount, result);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTransactionOverviewAsync error, type={Type}", request.Type);
        }
        
        return result;
    }

    public async Task<GetVolumeOverviewResult> GetVolumeOverviewAsync(GetOverviewRequestDto request)
    {
        var result = new GetVolumeOverviewResult();
        try
        {
            result.Volume.Latest = DateTime.UtcNow.Date.ToUtcString(TimeHelper.DatePattern);
            result.Volume.TotalAmountUsd = await GetOrderAmountAsync();

            var dateDimension = (DateDimensionEnum)Enum.Parse(typeof(DateDimensionEnum), request.Type.ToString());
            switch (dateDimension)
            {
                case DateDimensionEnum.Day:
                    result = await QuerySumAggAsync(DateInterval.Day, request.MaxResultCount, result);
                    break;
                case DateDimensionEnum.Week:
                    result = await QuerySumAggAsync(DateInterval.Week, request.MaxResultCount, result);
                    break;
                case DateDimensionEnum.Month:
                    result = await QuerySumAggAsync(DateInterval.Month, request.MaxResultCount, result);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetVolumeOverviewAsync error, type={Type}", request.Type);
        }

        return result;
    }

    public async Task<GetTokenResultDto> GetTokensAsync(GetTokenRequestDto request)
    {
        var result = new GetTokenResultDto();
        try
        {
            
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokensAsync error, type={Type}", request.Type);
        }
        
        return result;
    }

    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        try
        {
            var networkInfos = _networkOptions.Value.NetworkMap[CommonConstant.Symbol.USDT].Select(config =>
                config.NetworkInfo).ToList();
            var configs = _tokenOptions.Value.Deposit[ChainId.AELF];
            var result = new GetTokenOptionResultDto
            {
                NetworkList = _objectMapper.Map<List<NetworkInfo>, List<NetworkOptionDto>>(networkInfos),
                TokenList = _objectMapper.Map<List<TokenConfig>, List<TokenConfigOptionDto>>(configs)
            };
            return await LoopCollectionItemsAsync(result);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetNetworkOptionAsync error.");
            return new GetTokenOptionResultDto();
        }
    }

    public async Task<PagedResultDto<OrderIndexDto>> GetTransfersAsync(GetTransferRequestDto request)
    {
        try
        {
            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Terms(i =>
                i.Field(f => f.Status).Terms(new List<string>
                {
                    OrderStatusEnum.ToTransferConfirmed.ToString(),
                    OrderStatusEnum.Finish.ToString()
                })));
            
            if (request.Type > 0)
            {
                mustQuery.Add(q => q.Term(i =>
                    i.Field(f => f.OrderType)
                        .Value(Enum.GetName(typeof(OrderTypeEnum), request.Type))));
            }

            if (request.FromToken > 0 || request.FromChainId > 0 || request.ToToken > 0 || request.ToChainId > 0)
            {
                var options = await GetNetworkOptionAsync();
                if (request.FromToken > 0)
                {
                    var fromToken = options.TokenList.FirstOrDefault(t => t.Key == request.FromToken)?.Symbol;
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.FromTransfer.Symbol).Value(fromToken)));
                }
                if (request.ToToken > 0)
                {
                    var toToken = options.TokenList.FirstOrDefault(t => t.Key == request.ToToken)?.Symbol;
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.ToTransfer.Symbol).Value(toToken)));
                }
                if (request.FromChainId > 0)
                {
                    var fromChainId = options.NetworkList.FirstOrDefault(t => t.Key == request.FromChainId)?.Network;
                    if (fromChainId == ChainId.AELF || fromChainId == ChainId.tDVV || fromChainId == ChainId.tDVW)
                    {
                        mustQuery.Add(q => q.Term(i =>
                            i.Field(f => f.FromTransfer.ChainId).Value(fromChainId)));
                    }
                    else
                    {
                        mustQuery.Add(q => q.Term(i =>
                            i.Field(f => f.FromTransfer.Network).Value(fromChainId)));
                    }
                }
                if (request.ToChainId > 0)
                {
                    var toChainId = options.NetworkList.FirstOrDefault(t => t.Key == request.ToChainId)?.Network;
                    if (toChainId == ChainId.AELF || toChainId == ChainId.tDVV || toChainId == ChainId.tDVW)
                    {
                        mustQuery.Add(q => q.Term(i =>
                            i.Field(f => f.ToTransfer.ChainId).Value(toChainId)));
                    }
                    else
                    {
                        mustQuery.Add(q => q.Term(i =>
                            i.Field(f => f.ToTransfer.Network).Value(toChainId)));
                    }
                }
            }

            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

            var (count, list) = await _orderIndexRepository.GetSortListAsync(Filter,
                sortFunc: string.IsNullOrWhiteSpace(request.Sorting)
                    ? s => s.Descending(t => t.CreateTime)
                    : GetSorting(request.Sorting),
                limit: request.MaxResultCount == 0 ? OrderOptions.DefaultResultCount :
                request.MaxResultCount > request.Limit ? request.Limit : request.MaxResultCount,
                skip: request.SkipCount);

            return new PagedResultDto<OrderIndexDto>
            {
                Items = await LoopCollectionItemsAsync(
                    _objectMapper.Map<List<OrderIndex>, List<OrderIndexDto>>(list)),
                TotalCount = count
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTransfersAsync error, type={Type}", request.Type);
            return new PagedResultDto<OrderIndexDto>();
        }
    }

    private async Task<long> GetOrderCountAsync()
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Status).Terms(new List<string>
            {
                OrderStatusEnum.ToTransferConfirmed.ToString(),
                OrderStatusEnum.Finish.ToString()
            })));
        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        var countResponse = await _orderIndexRepository.CountAsync(Filter);
        return countResponse.Count;
    }

    private async Task<string> GetOrderAmountAsync()
    {
        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(q => q.Terms(i =>
                i.Field(f => f.Status).Terms(new List<string>
                {
                    OrderStatusEnum.ToTransferConfirmed.ToString(),
                    OrderStatusEnum.Finish.ToString()
                })))
            .Aggregations(symbolAgg => symbolAgg
                .Terms("symbol", terms => terms
                    .Field(f => f.FromTransfer.Symbol)
                    .Aggregations(sumAgg => sumAgg
                        .Sum("sum_amount", sum => sum
                            .Field(f => f.FromTransfer.Amount))
                    )
                )
            );
        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
            _logger.LogError("GetOrderAmountAsync error: {error}", searchResponse.ServerError?.Error);
        
        var agg = searchResponse.Aggregations.Terms("symbol");
        var amountUsd = 0M;
        foreach (var bucket in agg.Buckets)
        {
            var avgExchange =
                await _networkAppService.GetAvgExchangeAsync(bucket.Key, CommonConstant.Symbol.USD);
            amountUsd += (decimal)bucket.Sum("sum_amount")?.Value * avgExchange;
        }
        return amountUsd.ToString(2, DecimalHelper.RoundingOption.Floor);
    }

    private async Task<GetTransactionOverviewResult> QueryCountAggAsync(DateInterval dateInterval, int? maxResultCount,
        GetTransactionOverviewResult result)
    {
        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(q => q.Terms(i =>
                i.Field(f => f.Status).Terms(new List<string>
                {
                    OrderStatusEnum.ToTransferConfirmed.ToString(),
                    OrderStatusEnum.Finish.ToString()
                })))
            .Aggregations(a => a
                .DateHistogram("date", dh => dh
                    .Field(f => f.CreateTime)
                    .CalendarInterval(dateInterval)
                    .Order(HistogramOrder.KeyDescending)
                    .Aggregations(agg => agg
                        .Terms("order_type", ts => ts
                            .Field(f => f.OrderType)
                        )
                    )
                )
            );
        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
            _logger.LogError("QueryCountAggAsync error: {error}", searchResponse.ServerError?.Error);

        var agg = searchResponse.Aggregations.Histogram("date");
        foreach (var bucket in agg.Buckets)
        {
            var item = new OrderTxOverview
            {
                Date = GetDateString(dateInterval, (long)bucket.Key),
                DepositTx = 0L,
                WithdrawTx = 0L
            };

            var orderTypeAgg = bucket.Terms("order_type");
            foreach (var orderTypeBucket in orderTypeAgg.Buckets)
            {
                if (orderTypeBucket.Key == OrderTypeEnum.Deposit.ToString())
                {
                    item.DepositTx = orderTypeBucket.DocCount.Value;
                }
                else
                {
                    item.WithdrawTx = orderTypeBucket.DocCount.Value;
                }
            }
            
            switch (dateInterval)
            {
                case DateInterval.Day:
                    result.Transaction.Day.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Day = result.Transaction.Day.Take(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Week:
                    result.Transaction.Week.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Week = result.Transaction.Week.Take(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Month:
                    result.Transaction.Month.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Month = result.Transaction.Month.Take(maxResultCount.Value).ToList();
                    break;
            }
        }

        return result;
    }
    
    private async Task<GetVolumeOverviewResult> QuerySumAggAsync(DateInterval dateInterval, int? maxResultCount,
        GetVolumeOverviewResult result)
    {
        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(q => q.Terms(i =>
                i.Field(f => f.Status).Terms(new List<string>
                {
                    OrderStatusEnum.ToTransferConfirmed.ToString(),
                    OrderStatusEnum.Finish.ToString()
                })))
            .Aggregations(a => a
                .DateHistogram("date", dh => dh
                    .Field(f => f.CreateTime)
                    .CalendarInterval(dateInterval)
                    .Order(HistogramOrder.KeyDescending)
                    .Aggregations(agg => agg
                        .Terms("order_type", ts => ts
                            .Field(f => f.OrderType)
                            .Aggregations(symbolAgg => symbolAgg
                                .Terms("symbol", terms => terms
                                    .Field(f => f.FromTransfer.Symbol)
                                    .Aggregations(sumAgg => sumAgg
                                    .Sum("sum_amount", sum => sum
                                        .Field(f => f.FromTransfer.Amount))
                                    )
                                )
                            )
                        )
                    )
                )
            );
        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
            _logger.LogError("QuerySumAggAsync error: {error}", searchResponse.ServerError?.Error);

        var agg = searchResponse.Aggregations.Histogram("date");
        foreach (var bucket in agg.Buckets)
        {
            var item = new OrderVolumeOverview
            {
                Date = GetDateString(dateInterval, (long)bucket.Key),
                DepositAmountUsd = "0",
                WithdrawAmountUsd = "0"
            };
            var depositAmountUsd = 0M;
            var withdrawAmountUsd = 0M;
            
            var orderTypeAgg = bucket.Terms("order_type");
            foreach (var orderTypeBucket in orderTypeAgg.Buckets)
            {
                var symbolAgg = orderTypeBucket.Terms("symbol");
                foreach (var symbolBucket in symbolAgg.Buckets)
                {
                    var avgExchange =
                        await _networkAppService.GetAvgExchangeAsync(symbolBucket.Key, CommonConstant.Symbol.USD, (long)bucket.Key);
                    
                    if (orderTypeBucket.Key == OrderTypeEnum.Deposit.ToString())
                    {
                        depositAmountUsd += (decimal)symbolBucket.Sum("sum_amount")?.Value * avgExchange;
                    }
                    else
                    {
                        withdrawAmountUsd += (decimal)symbolBucket.Sum("sum_amount")?.Value * avgExchange;
                    }
                }
            }

            item.DepositAmountUsd = depositAmountUsd.ToString(2, DecimalHelper.RoundingOption.Floor);
            item.WithdrawAmountUsd = withdrawAmountUsd.ToString(2, DecimalHelper.RoundingOption.Floor);
            
            switch (dateInterval)
            {
                case DateInterval.Day:
                    result.Volume.Day.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Day = result.Volume.Day.Take(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Week:
                    result.Volume.Week.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Week = result.Volume.Week.Take(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Month:
                    result.Volume.Month.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Month = result.Volume.Month.Take(maxResultCount.Value).ToList();
                    break;
            }
        }

        return result;
    }

    private string GetDateString(DateInterval dateInterval, long timeStamp)
    {
        var date = TimeHelper.GetDateTimeFromTimeStamp(timeStamp).Date.ToUtcString(TimeHelper.DatePattern);
        switch (dateInterval)
        {
            case DateInterval.Month:
                date = TimeHelper.GetDateTimeFromTimeStamp(timeStamp).Date.ToUtcString(TimeHelper.DateMonthPattern);
                break;
        }
    
        return date;
    }
    
    private async Task<GetTokenOptionResultDto> LoopCollectionItemsAsync(GetTokenOptionResultDto dto)
    {
        var index = 0;
        foreach (var item in dto.NetworkList)
        {
            item.Key = ++index;
        }
        
        dto.NetworkList.Insert(0, new NetworkOptionDto
        {
            Key = 0,
            Name = CommonConstant.DefaultConst.All,
            Network = CommonConstant.DefaultConst.All
        });
        index = 0;
        foreach (var item in dto.TokenList)
        {
            item.Key = ++index;
        }
        dto.TokenList.Insert(0, new TokenConfigOptionDto()
        {
            Key = 0,
            Name = CommonConstant.DefaultConst.All,
            Symbol = CommonConstant.DefaultConst.All
        });
        return dto;
    }

    private async Task<List<OrderIndexDto>> LoopCollectionItemsAsync(List<OrderIndexDto> itemList)
    {
        var fromSymbolList = itemList.Select(i => i.FromTransfer.Symbol).Distinct().ToList();
        var toSymbolList = itemList.Select(i => i.ToTransfer.Symbol).Distinct().ToList();
        fromSymbolList.AddRange(toSymbolList);
        fromSymbolList = fromSymbolList.Distinct().ToList();
        var exchangeDic = new Dictionary<string, decimal>();
        foreach (var item in fromSymbolList)
        {
            exchangeDic.Add(item, await _networkAppService.GetAvgExchangeAsync(item, CommonConstant.Symbol.USD));
        }

        itemList.ForEach(item =>
        {
            item.FromTransfer.Status = ThirdPartOrderStatusEnum.Success.ToString();
            item.ToTransfer.Status = ThirdPartOrderStatusEnum.Success.ToString();
            item.Status = ThirdPartOrderStatusEnum.Success.ToString();
            item.FromTransfer.Amount =
                decimal.Parse(item.FromTransfer.Amount).ToString(4, DecimalHelper.RoundingOption.Floor);
            item.FromTransfer.AmountUsd =
                (decimal.Parse(item.FromTransfer.Amount) * exchangeDic[item.FromTransfer.Symbol]).ToString(2,
                    DecimalHelper.RoundingOption.Floor);
            item.ToTransfer.Amount =
                decimal.Parse(item.ToTransfer.Amount).ToString(4, DecimalHelper.RoundingOption.Floor);
            item.ToTransfer.AmountUsd =
                (decimal.Parse(item.ToTransfer.Amount) * exchangeDic[item.ToTransfer.Symbol]).ToString(2,
                    DecimalHelper.RoundingOption.Floor);
        });
        return itemList;
    }

    private static Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>> GetSorting(string sorting)
    {
        var result =
            new Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>>(s =>
                s.Descending(t => t.CreateTime));

        var sortingArray = sorting.Trim().ToLower().Split(CommonConstant.Space, StringSplitOptions.RemoveEmptyEntries);
        switch (sortingArray.Length)
        {
            case 1:
                switch (sortingArray[0])
                {
                    case OrderOptions.CreateTime:
                        result = s =>
                            s.Ascending(t => t.CreateTime);
                        break;
                }
                break;
            case 2:
                switch (sortingArray[0])
                {
                    case OrderOptions.CreateTime:
                        result = s =>
                            sortingArray[1] == OrderOptions.Asc || sortingArray[1] == OrderOptions.Ascend
                                ? s.Ascending(t => t.CreateTime)
                                : s.Descending(t => t.CreateTime);
                        break;
                }
                break;
        }

        return result;
    }
}