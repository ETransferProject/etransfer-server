using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Options;
using ETransferServer.Orders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.Order;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class OrderAppService : ApplicationService, IOrderAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;
    private readonly NetworkOptions _networkOptions;
    private readonly ChainOptions _chainOptions;
    
    public OrderAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger, 
        IOptionsSnapshot<NetworkOptions> networkOptions,
        IOptionsSnapshot<ChainOptions> chainOptions)
    {
        _orderIndexRepository = orderIndexRepository;
        _objectMapper = objectMapper;
        _logger = logger;
        _networkOptions = networkOptions.Value;
        _chainOptions = chainOptions.Value;
    }

    public async Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request)
    {
        try
        {
            var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
            if (!userId.HasValue || userId == Guid.Empty) return new PagedResultDto<OrderIndexDto>();

            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userId.ToString())));

            if (request.Type > 0)
            {
                mustQuery.Add(q => q.Term(i =>
                    i.Field(f => f.OrderType)
                        .Value(Enum.GetName(typeof(OrderTypeEnum), request.Type))));
            }

            if (request.Status > 0)
            {
                var status =
                    (OrderStatusResponseEnum)Enum.Parse(typeof(OrderStatusResponseEnum), request.Status.ToString());
                switch (status)
                {
                    case OrderStatusResponseEnum.Processing:
                        mustQuery.Add(q => q.Terms(i =>
                            i.Field(f => f.Status).Terms(new List<string>
                            {
                                OrderStatusEnum.Initialized.ToString(),
                                OrderStatusEnum.Created.ToString(),
                                OrderStatusEnum.Pending.ToString(),
                                OrderStatusEnum.FromStartTransfer.ToString(),
                                OrderStatusEnum.FromTransferring.ToString(),
                                OrderStatusEnum.FromTransferred.ToString(),
                                OrderStatusEnum.FromTransferConfirmed.ToString(),
                                OrderStatusEnum.ToStartTransfer.ToString(),
                                OrderStatusEnum.ToTransferring.ToString(),
                                OrderStatusEnum.ToTransferred.ToString(),
                                OrderStatusEnum.ToTransferConfirmed.ToString()
                            })));
                        break;
                    case OrderStatusResponseEnum.Succeed:
                        mustQuery.Add(q => q.Term(i =>
                            i.Field(f => f.Status).Value(OrderStatusEnum.Finish.ToString())));
                        break;
                    case OrderStatusResponseEnum.Failed:
                        mustQuery.Add(q => q.Terms(i =>
                            i.Field(f => f.Status).Terms(new List<string>
                            {
                                OrderStatusEnum.FromTransferFailed.ToString(),
                                OrderStatusEnum.ToTransferFailed.ToString(),
                                OrderStatusEnum.Expired.ToString(),
                                OrderStatusEnum.Failed.ToString()
                            })));
                        break;
                }
            }

            if (request.StartTimestamp.HasValue)
            {
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.ArrivalTime)
                        .GreaterThanOrEquals(request.StartTimestamp.Value)));
            }

            if (request.EndTimestamp.HasValue)
            {
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.ArrivalTime)
                        .LessThanOrEquals(request.EndTimestamp.Value)));
            }

            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

            List<OrderIndexDto> itemList;
            if (!string.IsNullOrWhiteSpace(request.Sorting))
            {
                var sorting = GetSorting(request.Sorting);
                var list = await _orderIndexRepository.GetSortListAsync(Filter,
                    sortFunc: sorting,
                    limit: !request.MaxResultCount.HasValue ? OrderOptions.DefaultResultCount :
                    request.MaxResultCount.Value > OrderOptions.MaxResultCount ? OrderOptions.MaxResultCount :
                    request.MaxResultCount.Value,
                    skip: !request.SkipCount.HasValue ? OrderOptions.DefaultSkipCount : request.SkipCount.Value);
                itemList = _objectMapper.Map<List<OrderIndex>, List<OrderIndexDto>>(list.Item2);
            }
            else
            {
                var list = await _orderIndexRepository.GetSortListAsync(Filter,
                    sortFunc: s => s.Descending(t => t.ArrivalTime),
                    limit: !request.MaxResultCount.HasValue ? OrderOptions.DefaultResultCount :
                    request.MaxResultCount.Value > OrderOptions.MaxResultCount ? OrderOptions.MaxResultCount :
                    request.MaxResultCount.Value,
                    skip: !request.SkipCount.HasValue ? OrderOptions.DefaultSkipCount : request.SkipCount.Value);
                itemList = _objectMapper.Map<List<OrderIndex>, List<OrderIndexDto>>(list.Item2);
            }

            var totalCount = await _orderIndexRepository.CountAsync(Filter);

            return new PagedResultDto<OrderIndexDto>
            {
                Items = await LoopCollectionItemsAsync(itemList),
                TotalCount = totalCount.Count
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get order record list failed, type={Type}, status={Status}", 
                request.Type, request.Status);
            return new PagedResultDto<OrderIndexDto>();
        }
    }

    public async Task<OrderStatusDto> GetOrderRecordStatusAsync()
    {
        try
        {
            var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
            if (!userId.HasValue || userId == Guid.Empty) return new OrderStatusDto();
            
            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userId.ToString())));
            
            mustQuery.Add(q => q.Terms(i => 
                i.Field(f => f.Status).Terms(new List<string>
                {
                    OrderStatusEnum.Initialized.ToString(),
                    OrderStatusEnum.Created.ToString(),
                    OrderStatusEnum.Pending.ToString(),
                    OrderStatusEnum.FromStartTransfer.ToString(),
                    OrderStatusEnum.FromTransferring.ToString(),
                    OrderStatusEnum.FromTransferred.ToString(),
                    OrderStatusEnum.FromTransferConfirmed.ToString(),
                    OrderStatusEnum.ToStartTransfer.ToString(),
                    OrderStatusEnum.ToTransferring.ToString(),
                    OrderStatusEnum.ToTransferred.ToString(),
                    OrderStatusEnum.ToTransferConfirmed.ToString()
                })));
            
            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
            
            var countResponse = await _orderIndexRepository.CountAsync(Filter);
            return new OrderStatusDto
            {
                Status = countResponse.Count > 0
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get order record status failed");
            return new OrderStatusDto();
        }
    }

    private static Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>> GetSorting(string sorting)
    {
        var result =
            new Func<SortDescriptor<OrderIndex>, IPromise<IList<ISort>>>(s =>
                s.Descending(t => t.ArrivalTime));
        
        var sortingArray = sorting.Trim().ToLower().Split(CommonConstant.Space, StringSplitOptions.RemoveEmptyEntries);
        switch (sortingArray.Length)
        {
            case 1:
                switch (sortingArray[0])
                {
                    case OrderOptions.ArrivalTime:
                        result = s =>
                            s.Ascending(t => t.ArrivalTime);
                        break;
                }
                break;
            case 2:
                switch (sortingArray[0])
                {
                    case OrderOptions.ArrivalTime:
                        result = s =>
                            sortingArray[1] == OrderOptions.Asc || sortingArray[1] == OrderOptions.Ascend
                                ? s.Ascending(t => t.ArrivalTime)
                                : s.Descending(t => t.ArrivalTime);
                        break;
                }
                break;
        }
        
        return result;
    }

    private async Task<List<OrderIndexDto>> LoopCollectionItemsAsync(List<OrderIndexDto> itemList)
    {
        if (_networkOptions.NetworkMap.TryGetValue(CoBoConstant.Coins.USDT, out var networkConfigs))
        {
            AssertHelper.NotEmpty(networkConfigs, "Support symbol empty.");
        }
        
        itemList.ForEach(item =>
        {
            if (item.OrderType == OrderTypeEnum.Deposit.ToString())
            {
                item.FromTransfer.Icon = networkConfigs.FirstOrDefault(c => 
                    c.NetworkInfo.Network == item.FromTransfer.Network)?.NetworkInfo?.Icon;
                item.ToTransfer.Icon = _chainOptions.ChainInfos[item.ToTransfer.ChainId].Icon;
            }
            else
            {
                item.FromTransfer.Icon = _chainOptions.ChainInfos[item.FromTransfer.ChainId].Icon;
                item.ToTransfer.Icon = networkConfigs.FirstOrDefault(c => 
                    c.NetworkInfo.Network == item.ToTransfer.Network)?.NetworkInfo?.Icon;
            }

            var status = Enum.Parse<OrderStatusEnum>(item.Status);
            switch (status)
            {
                case OrderStatusEnum.Finish:
                    item.Status = OrderStatusResponseEnum.Succeed.ToString();
                    item.ArrivalTime = item.LastModifyTime;
                    break;
                case OrderStatusEnum.FromTransferFailed:
                case OrderStatusEnum.ToTransferFailed:
                case OrderStatusEnum.Expired:
                case OrderStatusEnum.Failed:
                    item.Status = OrderStatusResponseEnum.Failed.ToString();
                    item.ArrivalTime = 0;
                    break;
                default:
                    item.Status = OrderStatusResponseEnum.Processing.ToString();
                    break;
            }
        });
        return itemList;
    }
}