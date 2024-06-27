using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Options;
using ETransferServer.Orders;
using Microsoft.Extensions.Logging;
using Nest;
using Orleans;
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
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;

    public OrderAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger)
    {
        _orderIndexRepository = orderIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
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
                                OrderStatusEnum.ToTransferred.ToString()
                            })));
                        break;
                    case OrderStatusResponseEnum.Succeed:
                        mustQuery.Add(q => q.Terms(i =>
                            i.Field(f => f.Status).Terms(new List<string>
                            {
                                OrderStatusEnum.ToTransferConfirmed.ToString(),
                                OrderStatusEnum.Finish.ToString()
                            })));
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

            var (count, list) = await _orderIndexRepository.GetSortListAsync(Filter,
                sortFunc: string.IsNullOrWhiteSpace(request.Sorting)
                    ? s => s.Descending(t => t.ArrivalTime)
                    : GetSorting(request.Sorting),
                limit: request.MaxResultCount == 0 ? OrderOptions.DefaultResultCount :
                request.MaxResultCount > OrderOptions.MaxResultCount ? OrderOptions.MaxResultCount :
                request.MaxResultCount,
                skip: request.SkipCount);

            var orderIndexDtoPageResult = new PagedResultDto<OrderIndexDto>
            {
                Items = await LoopCollectionItemsAsync(
                    _objectMapper.Map<List<OrderIndex>, List<OrderIndexDto>>(list)),
                TotalCount = count
            };
            
            if (orderIndexDtoPageResult.Items.Any())
            {
                var maxCreateTime = orderIndexDtoPageResult.Items.Max(item => item.CreateTime);
                var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(userId.ToString());
                await userOrderActionGrain.AddOrUpdate(maxCreateTime);
            }
            
            return orderIndexDtoPageResult;
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
            if (!userId.HasValue || userId == Guid.Empty)
            {
                return new OrderStatusDto();
            }

            var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(userId.ToString());
            var lastModifyTime = await userOrderActionGrain.Get();

            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>
            {
                q => q.Term(i => i.Field(f => f.UserId).Value(userId.ToString())),
                q => q.Range(i => i.Field(f => f.CreateTime).GreaterThan(lastModifyTime)),
                q => q.Range(i => i.Field(f => f.CreateTime).GreaterThan(DateTime.UtcNow.AddDays(OrderOptions.ValidOrderThreshold).ToUtcMilliSeconds())),
                q => q.Terms(i => i.Field(f => f.Status).Terms((IEnumerable<string>)new List<string>
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
                    OrderStatusEnum.ToTransferred.ToString()
                }))
            };

            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
            
            var count = await _orderIndexRepository.CountAsync(Filter);
            
            return new OrderStatusDto
            {
                Status = count.Count > 0
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
        itemList.ForEach(item =>
        {
            var status = Enum.Parse<OrderStatusEnum>(item.Status);
            switch (status)
            {
                case OrderStatusEnum.ToTransferConfirmed:
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