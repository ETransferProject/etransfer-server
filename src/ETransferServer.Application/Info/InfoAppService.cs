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

            var dateDimension = (DateDimensionEnum)Enum.Parse(typeof(DateDimensionEnum), request.Type);
            switch (dateDimension)
            {
                case DateDimensionEnum.Day:
                    result = await QueryCountAggAsync(DateInterval.Day, request.MaxResultCount, result);
                    result.Transaction.TotalTx = result.Transaction.Day.Sum(t => t.DepositTx + t.WithdrawTx);
                    break;
                case DateDimensionEnum.Week:
                    result = await QueryCountAggAsync(DateInterval.Week, request.MaxResultCount, result);
                    result.Transaction.TotalTx = result.Transaction.Week.Sum(t => t.DepositTx + t.WithdrawTx);
                    break;
                case DateDimensionEnum.Month:
                    result = await QueryCountAggAsync(DateInterval.Month, request.MaxResultCount, result);
                    result.Transaction.TotalTx = result.Transaction.Month.Sum(t => t.DepositTx + t.WithdrawTx);
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTransactionOverviewAsync error, type={type}", request.Type);
        }
        
        return result;
    }

    public async Task<GetVolumeOverviewResult> GetVolumeOverviewAsync(GetOverviewRequestDto request)
    {
        var result = new GetVolumeOverviewResult();
        try
        {
            result.Volume.Latest = DateTime.UtcNow.Date.ToUtcString(TimeHelper.DatePattern);

            var dateDimension = (DateDimensionEnum)Enum.Parse(typeof(DateDimensionEnum), request.Type);
            switch (dateDimension)
            {
                case DateDimensionEnum.Day:
                    result = await QuerySumAggAsync(DateInterval.Day, request.MaxResultCount, result);
                    result.Volume.TotalAmountUsd = result.Volume.Day
                        .Sum(t => t.DepositAmountUsd.SafeToDecimal() + t.WithdrawAmountUsd.SafeToDecimal()).ToString();
                    break;
                case DateDimensionEnum.Week:
                    result = await QuerySumAggAsync(DateInterval.Week, request.MaxResultCount, result);
                    result.Volume.TotalAmountUsd = result.Volume.Week
                        .Sum(t => t.DepositAmountUsd.SafeToDecimal() + t.WithdrawAmountUsd.SafeToDecimal()).ToString();
                    break;
                case DateDimensionEnum.Month:
                    result = await QuerySumAggAsync(DateInterval.Month, request.MaxResultCount, result);
                    result.Volume.TotalAmountUsd = result.Volume.Month
                        .Sum(t => t.DepositAmountUsd.SafeToDecimal() + t.WithdrawAmountUsd.SafeToDecimal()).ToString();
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetVolumeOverviewAsync error, type={type}", request.Type);
        }

        return result;
    }

    public async Task<GetTokenResultDto> GetTokensAsync(GetTokenRequestDto request)
    {
        var result = new GetTokenResultDto();
        try
        {
            var orderType = Enum.GetName(typeof(OrderTypeEnum), int.Parse(request.Type));
            if (orderType.IsNullOrEmpty() || orderType == OrderTypeEnum.Deposit.ToString())
            {
                result = await GetTokenAmountAsync(DateRangeEnum._24H, OrderTypeEnum.Deposit, string.Empty, result);
                result = await GetTokenAmountAsync(DateRangeEnum._7D, OrderTypeEnum.Deposit, string.Empty, result);
                result = await GetTokenAmountAsync(DateRangeEnum.Total, OrderTypeEnum.Deposit, string.Empty, result);
            }

            if (orderType.IsNullOrEmpty() || orderType == OrderTypeEnum.Withdraw.ToString())
            {
                result = await GetTokenAmountAsync(DateRangeEnum._24H, OrderTypeEnum.Withdraw, string.Empty, result);
                result = await GetTokenAmountAsync(DateRangeEnum._7D, OrderTypeEnum.Withdraw, string.Empty, result);
                result = await GetTokenAmountAsync(DateRangeEnum.Total, OrderTypeEnum.Withdraw, string.Empty, result);
                result = await GetTokenAmountAsync(DateRangeEnum._24H, OrderTypeEnum.Withdraw, ChainId.AELF, result);
                result = await GetTokenAmountAsync(DateRangeEnum._24H, OrderTypeEnum.Withdraw, ChainId.tDVV, result);
                result = await GetTokenAmountAsync(DateRangeEnum._24H, OrderTypeEnum.Withdraw, ChainId.tDVW, result);
                result = await GetTokenAmountAsync(DateRangeEnum._7D, OrderTypeEnum.Withdraw, ChainId.AELF, result);
                result = await GetTokenAmountAsync(DateRangeEnum._7D, OrderTypeEnum.Withdraw, ChainId.tDVV, result);
                result = await GetTokenAmountAsync(DateRangeEnum._7D, OrderTypeEnum.Withdraw, ChainId.tDVW, result);
                result = await GetTokenAmountAsync(DateRangeEnum.Total, OrderTypeEnum.Withdraw, ChainId.AELF, result);
                result = await GetTokenAmountAsync(DateRangeEnum.Total, OrderTypeEnum.Withdraw, ChainId.tDVV, result);
                result = await GetTokenAmountAsync(DateRangeEnum.Total, OrderTypeEnum.Withdraw, ChainId.tDVW, result);
            }

            var tokenConfigs = _tokenOptions.Value.Deposit[ChainId.AELF];
            foreach (var kvp in result)
            {
                var networkConfigs = _networkOptions.Value.NetworkMap[kvp.Key];
                var networks = networkConfigs.Select(config => config.NetworkInfo.Network).ToList();
                var names = result[kvp.Key].Details.Select(d => d.Name).ToList();
                result[kvp.Key].Icon = tokenConfigs.FirstOrDefault(t => t.Symbol == kvp.Key)?.Icon;
                result[kvp.Key].Networks = networkConfigs.Where(n => names.Contains(n.NetworkInfo.Network))
                    .Select(t => t.NetworkInfo.Network).ToList();
                result[kvp.Key].ChainIds = networkConfigs
                    .Where(n => names.Contains(n.NetworkInfo.Network) && (n.NetworkInfo.Network == ChainId.AELF ||
                                                                          n.NetworkInfo.Network == ChainId.tDVV ||
                                                                          n.NetworkInfo.Network == ChainId.tDVW))
                    .Select(t => t.NetworkInfo.Network).ToList();
                result[kvp.Key].General.Amount24H = kvp.Value.Details.Sum(d => d.Item.Amount24H.SafeToDecimal())
                    .ToString(4, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].General.Amount24HUsd = kvp.Value.Details.Sum(d => d.Item.Amount24HUsd.SafeToDecimal())
                    .ToString(2, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].General.Amount7D = kvp.Value.Details.Sum(d => d.Item.Amount7D.SafeToDecimal())
                    .ToString(4, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].General.Amount7DUsd = kvp.Value.Details.Sum(d => d.Item.Amount7DUsd.SafeToDecimal())
                    .ToString(2, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].General.AmountTotal = kvp.Value.Details.Sum(d => d.Item.AmountTotal.SafeToDecimal())
                    .ToString(4, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].General.AmountTotalUsd = kvp.Value.Details
                    .Sum(d => d.Item.AmountTotalUsd.SafeToDecimal())
                    .ToString(2, DecimalHelper.RoundingOption.Floor);
                result[kvp.Key].Details = kvp.Value.Details.OrderBy(d => networks.IndexOf(d.Name)).ToList();
            }

            return result.OrderByDescending(kv => kv.Value.General.AmountTotalUsd.SafeToDecimal())
                .ToDictionary(kv => kv.Key, kv => kv.Value).ToDictionary();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetTokensAsync error, type={type}", request.Type);
        }

        return result;
    }

    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        try
        {
            var networkInfos = _networkOptions.Value.NetworkMap[CommonConstant.Symbol.USDT].Select(config =>
                config.NetworkInfo).ToList();
            var tokenConfigs = _tokenOptions.Value.Deposit[ChainId.AELF];
            var result = new GetTokenOptionResultDto
            {
                NetworkList = _objectMapper.Map<List<NetworkInfo>, List<NetworkOptionDto>>(networkInfos),
                TokenList = _objectMapper.Map<List<TokenConfig>, List<TokenConfigOptionDto>>(tokenConfigs)
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
            
            if (int.Parse(request.Type) > 0)
            {
                mustQuery.Add(q => q.Term(i =>
                    i.Field(f => f.OrderType)
                        .Value(Enum.GetName(typeof(OrderTypeEnum), int.Parse(request.Type)))));
            }

            if (request.FromToken > 0 || request.FromChainId > 0 || request.ToToken > 0 || request.ToChainId > 0)
            {
                var options = await GetNetworkOptionAsync();
                if (request.FromToken > 0)
                {
                    var fromToken = options.TokenList.FirstOrDefault(t => t.Key == request.FromToken)?.Symbol;
                    if (fromToken.IsNullOrEmpty()) return new PagedResultDto<OrderIndexDto>();
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.FromTransfer.Symbol).Value(fromToken)));
                }

                if (request.ToToken > 0)
                {
                    var toToken = options.TokenList.FirstOrDefault(t => t.Key == request.ToToken)?.Symbol;
                    if (toToken.IsNullOrEmpty()) return new PagedResultDto<OrderIndexDto>();
                    mustQuery.Add(q => q.Term(i =>
                        i.Field(f => f.ToTransfer.Symbol).Value(toToken)));
                }

                if (request.FromChainId > 0)
                {
                    var fromChainId = options.NetworkList.FirstOrDefault(t => t.Key == request.FromChainId)?.Network;
                    if (fromChainId.IsNullOrEmpty()) return new PagedResultDto<OrderIndexDto>();
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
                    if (toChainId.IsNullOrEmpty()) return new PagedResultDto<OrderIndexDto>();
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
                TotalCount = count <= request.Limit ? count : request.Limit
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "GetTransfersAsync error, type={type}, fromToken={fromToken}, fromChainId={fromChainId}, toToken={toToken}, toChainId={toChainId}, ",
                request.Type, request.FromToken, request.FromChainId, request.ToToken, request.ToChainId);
            return new PagedResultDto<OrderIndexDto>();
        }
    }

    private async Task<GetTokenResultDto> GetTokenAmountAsync(DateRangeEnum dateRangeEnum, OrderTypeEnum orderTypeEnum,
        string chainId, GetTokenResultDto result)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>
        {
            q => q.Terms(i => i.Field(f => f.Status).Terms(new List<string>
            {
                OrderStatusEnum.ToTransferConfirmed.ToString(),
                OrderStatusEnum.Finish.ToString()
            }))
        };
        switch (dateRangeEnum)
        {
            case DateRangeEnum._24H:
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.CreateTime).GreaterThan(DateTime.UtcNow.AddDays(-1).ToUtcMilliSeconds())));
                break;
            case DateRangeEnum._7D:
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.CreateTime).GreaterThan(DateTime.UtcNow.AddDays(-7).ToUtcMilliSeconds())));
                break;
        }

        if (!chainId.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.ToTransfer.ChainId).Value(chainId)));
        }

        var s = new SearchDescriptor<OrderIndex>()
            .Size(0)
            .Query(q => q
                .Bool(b => b
                    .Must(mustQuery)
                )
            );

        switch (orderTypeEnum)
        {
            case OrderTypeEnum.Deposit:
                s.Aggregations(agg => agg
                    .Terms("symbol", ts => ts
                        .Field(f => f.FromTransfer.Symbol)
                        .Aggregations(networkAgg => networkAgg
                            .Terms("network", terms => terms
                                .Field(f => f.FromTransfer.Network)
                                .Aggregations(sumAgg => sumAgg
                                    .Sum("sum_amount", sum => sum
                                        .Field(f => f.FromTransfer.Amount))
                                )
                            )
                        )
                    )
                );
                break;
            case OrderTypeEnum.Withdraw:
                if (chainId.IsNullOrEmpty())
                    s.Aggregations(agg => agg
                        .Terms("symbol", ts => ts
                            .Field(f => f.FromTransfer.Symbol)
                            .Aggregations(networkAgg => networkAgg
                                .Terms("network", terms => terms
                                    .Field(f => f.ToTransfer.Network)
                                    .Aggregations(sumAgg => sumAgg
                                        .Sum("sum_amount", sum => sum
                                            .Field(f => f.FromTransfer.Amount))
                                    )
                                )
                            )
                        )
                    );
                else
                    s.Aggregations(agg => agg
                        .Terms("symbol", ts => ts
                            .Field(f => f.FromTransfer.Symbol)
                            .Aggregations(networkAgg => networkAgg
                                .Terms("network", terms => terms
                                    .Field(f => f.ToTransfer.ChainId)
                                    .Aggregations(sumAgg => sumAgg
                                        .Sum("sum_amount", sum => sum
                                            .Field(f => f.FromTransfer.Amount))
                                    )
                                )
                            )
                        )
                    );

                break;
        }

        var searchResponse = await _orderIndexRepository.SearchAsync(s, 0, 0);
        if (!searchResponse.IsValid)
            _logger.LogError("GetTokenAmountAsync error: {error}", searchResponse.ServerError?.Error);

        var amountUsd = 0M;

        var symbolAgg = searchResponse.Aggregations.Terms("symbol");
        foreach (var symbolBucket in symbolAgg.Buckets)
        {
            var avgExchange = 0M;
            try
            {
                avgExchange =
                    await _networkAppService.GetAvgExchangeAsync(symbolBucket.Key, CommonConstant.Symbol.USD);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetTokenAmountAsync exchange error, {symbol}", symbolBucket.Key);
            }

            var networkAgg = symbolBucket.Terms("network");
            foreach (var networkBucket in networkAgg.Buckets)
            {
                if (chainId.IsNullOrEmpty() && orderTypeEnum == OrderTypeEnum.Withdraw &&
                    networkBucket.Key == ChainId.AELF)
                {
                    continue;
                }

                if (!result.ContainsKey(symbolBucket.Key))
                {
                    result.Add(symbolBucket.Key, new TokenResultDto());
                }

                var amount = (decimal)networkBucket.Sum("sum_amount")?.Value;
                var detail = result[symbolBucket.Key].Details.FirstOrDefault(d => d.Name == networkBucket.Key);
                if (detail == null)
                {
                    detail = new DetailDto
                    {
                        Name = networkBucket.Key
                    };
                    result[symbolBucket.Key].Details.Add(detail);
                }
                switch (dateRangeEnum)
                {
                    case DateRangeEnum._24H:
                        amount += detail.Item.Amount24H.IsNullOrEmpty() ? 0M : detail.Item.Amount24H.SafeToDecimal();
                        detail.Item.Amount24H = amount.ToString(4, DecimalHelper.RoundingOption.Floor);
                        detail.Item.Amount24HUsd = (amount * avgExchange).ToString(2, DecimalHelper.RoundingOption.Floor);
                        break;
                    case DateRangeEnum._7D:
                        amount += detail.Item.Amount7D.IsNullOrEmpty() ? 0M : detail.Item.Amount7D.SafeToDecimal();
                        detail.Item.Amount7D = amount.ToString(4, DecimalHelper.RoundingOption.Floor);
                        detail.Item.Amount7DUsd = (amount * avgExchange).ToString(2, DecimalHelper.RoundingOption.Floor);
                        break;
                    default:
                        amount += detail.Item.AmountTotal.IsNullOrEmpty() ? 0M : detail.Item.AmountTotal.SafeToDecimal();
                        detail.Item.AmountTotal = amount.ToString(4, DecimalHelper.RoundingOption.Floor);
                        detail.Item.AmountTotalUsd = (amount * avgExchange).ToString(2, DecimalHelper.RoundingOption.Floor);
                        break;
                }
            }
        }

        return result;
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
                    .Order(HistogramOrder.KeyAscending)
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
                    if (item.DepositTx > 0 || item.WithdrawTx > 0) result.Transaction.Day.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Day = result.Transaction.Day.TakeLast(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Week:
                    if (item.DepositTx > 0 || item.WithdrawTx > 0) result.Transaction.Week.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Week = result.Transaction.Week.TakeLast(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Month:
                    if (item.DepositTx > 0 || item.WithdrawTx > 0) result.Transaction.Month.Add(item);
                    if (maxResultCount.HasValue)
                        result.Transaction.Month = result.Transaction.Month.TakeLast(maxResultCount.Value).ToList();
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
                })));
        if (dateInterval == DateInterval.Day)
        {
            s.Aggregations(a => a
                .DateHistogram("date", dh => dh
                    .Field(f => f.CreateTime)
                    .CalendarInterval(dateInterval)
                    .Order(HistogramOrder.KeyAscending)
                    .Aggregations(agg => agg
                        .Terms("symbol", ts => ts
                            .Field(f => f.FromTransfer.Symbol)
                            .Aggregations(typeAgg => typeAgg
                                .Terms("order_type", terms => terms
                                    .Field(f => f.OrderType)
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
        }
        else
        {
            s.Aggregations(a => a
                .DateHistogram("date", dh => dh
                    .Field(f => f.CreateTime)
                    .CalendarInterval(dateInterval)
                    .Order(HistogramOrder.KeyAscending)
                    .Aggregations(agg => agg
                        .Terms("symbol", ts => ts
                            .Field(f => f.FromTransfer.Symbol)
                            .Aggregations(typeAgg => typeAgg
                                .Terms("order_type", terms => terms
                                    .Field(f => f.OrderType)
                                    .Aggregations(dAgg => dAgg
                                        .DateHistogram("dd", dd => dd
                                            .Field(f => f.CreateTime)
                                            .CalendarInterval(DateInterval.Day)
                                            .Order(HistogramOrder.KeyAscending)
                                            .Aggregations(sumAgg => sumAgg
                                                .Sum("sum_amount", sum => sum
                                                    .Field(f => f.FromTransfer.Amount))
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );
        }

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
            var depositDic = new Dictionary<long, decimal>();
            var withdrawDic = new Dictionary<long, decimal>();
            
            var symbolAgg = bucket.Terms("symbol");
            foreach (var symbolBucket in symbolAgg.Buckets)
            {
                var avgExchange = 0M;
                if (dateInterval == DateInterval.Day)
                {
                    try
                    {
                        avgExchange =
                            await _networkAppService.GetAvgExchangeAsync(symbolBucket.Key,
                                CommonConstant.Symbol.USD,
                                (long)bucket.Key);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "QuerySumAggAsync exchange error, {symbol}, {timestamp}",
                            symbolBucket.Key, bucket.Key);
                    }
                }
                
                var orderTypeAgg = symbolBucket.Terms("order_type");
                foreach (var orderTypeBucket in orderTypeAgg.Buckets)
                {
                    if (dateInterval == DateInterval.Day)
                    {
                        if (orderTypeBucket.Key == OrderTypeEnum.Deposit.ToString())
                        {
                            depositAmountUsd += (decimal)orderTypeBucket.Sum("sum_amount")?.Value * avgExchange;
                        }
                        else
                        {
                            withdrawAmountUsd += (decimal)orderTypeBucket.Sum("sum_amount")?.Value * avgExchange;
                        }
                    }
                    else
                    {
                        var ddAgg = orderTypeBucket.Histogram("dd");
                        foreach (var ddBucket in ddAgg.Buckets)
                        {
                            avgExchange = 0M;
                            var dd = (long)ddBucket.Key;
                            try
                            {
                                if (ddBucket.Sum("sum_amount")?.Value > 0)
                                {
                                    avgExchange =
                                        await _networkAppService.GetAvgExchangeAsync(symbolBucket.Key,
                                            CommonConstant.Symbol.USD,
                                            (long)ddBucket.Key);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "QuerySumAggAsync exchange error, {symbol}, {timestamp}", 
                                    symbolBucket.Key, bucket.Key);
                            }
                            if (orderTypeBucket.Key == OrderTypeEnum.Deposit.ToString())
                            {
                                if (depositDic.ContainsKey(dd))
                                    depositDic[dd] += (decimal)ddBucket.Sum("sum_amount")?.Value * avgExchange;
                                else
                                    depositDic.Add(dd, (decimal)ddBucket.Sum("sum_amount")?.Value * avgExchange);
                            }
                            else
                            {
                                if (withdrawDic.ContainsKey(dd))
                                    withdrawDic[dd] += (decimal)ddBucket.Sum("sum_amount")?.Value * avgExchange;
                                else
                                    withdrawDic.Add(dd, (decimal)ddBucket.Sum("sum_amount")?.Value * avgExchange);
                            }
                        }
                    }
                }
            }

            item.DepositAmountUsd = dateInterval == DateInterval.Day
                ? depositAmountUsd.ToString(2, DecimalHelper.RoundingOption.Floor)
                : depositDic.Sum(t => decimal.Parse(t.Value.ToString(2, DecimalHelper.RoundingOption.Floor)))
                    .ToString(2, DecimalHelper.RoundingOption.Floor);
            item.WithdrawAmountUsd = dateInterval == DateInterval.Day
                ? withdrawAmountUsd.ToString(2, DecimalHelper.RoundingOption.Floor)
                : withdrawDic.Sum(t => decimal.Parse(t.Value.ToString(2, DecimalHelper.RoundingOption.Floor)))
                    .ToString(2, DecimalHelper.RoundingOption.Floor);

            switch (dateInterval)
            {
                case DateInterval.Day:
                    if (decimal.Parse(item.DepositAmountUsd) > 0 || decimal.Parse(item.WithdrawAmountUsd) > 0)
                        result.Volume.Day.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Day = result.Volume.Day.TakeLast(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Week:
                    if (decimal.Parse(item.DepositAmountUsd) > 0 || decimal.Parse(item.WithdrawAmountUsd) > 0)
                        result.Volume.Week.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Week = result.Volume.Week.TakeLast(maxResultCount.Value).ToList();
                    break;
                case DateInterval.Month:
                    if (decimal.Parse(item.DepositAmountUsd) > 0 || decimal.Parse(item.WithdrawAmountUsd) > 0)
                        result.Volume.Month.Add(item);
                    if (maxResultCount.HasValue)
                        result.Volume.Month = result.Volume.Month.TakeLast(maxResultCount.Value).ToList();
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
        index = 0;
        foreach (var item in dto.TokenList)
        {
            item.Key = ++index;
        }
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
            try
            {
                exchangeDic.Add(item, await _networkAppService.GetAvgExchangeAsync(item, CommonConstant.Symbol.USD));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "LoopCollectionItemsAsync exchange error, {symbol}", item);
                exchangeDic.Add(item, 0M);
            }
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