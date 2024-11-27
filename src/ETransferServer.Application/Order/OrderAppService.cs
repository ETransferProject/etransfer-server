using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Entities;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Orders;
using ETransferServer.ThirdPart.CoBo.Dtos;
using ETransferServer.User.Dtos;
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
public partial class OrderAppService : ApplicationService, IOrderAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly INESTRepository<UserIndex, Guid> _userIndexRepository;
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;
    private readonly INetworkAppService _networkAppService;

    public OrderAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        INESTRepository<UserIndex, Guid> userIndexRepository,
        IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger,
        INetworkAppService networkAppService)
    {
        _orderIndexRepository = orderIndexRepository;
        _userIndexRepository = userIndexRepository;
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _networkAppService = networkAppService;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderAppService),
        MethodName = nameof(HandleGetListExceptionAsync))]
    public async Task<PagedResultDto<OrderIndexDto>> GetOrderRecordListAsync(GetOrderRecordRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if ((!userId.HasValue || userId == Guid.Empty) && request.AddressList.IsNullOrEmpty())
            return new PagedResultDto<OrderIndexDto>();

        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        if (userId.HasValue && request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userId.ToString())));
        }
        else if (userId.HasValue && !request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Term(k =>
                    k.Field(f => f.UserId).Value(userId.ToString())),
                s => s.Terms(k =>
                    k.Field(f => f.FromTransfer.FromAddress).Terms(request.AddressList)),
                s => s.Terms(k =>
                    k.Field(f => f.ToTransfer.ToAddress).Terms(request.AddressList)))));
        }
        else if (!request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Terms(k =>
                    k.Field(f => f.FromTransfer.FromAddress).Terms(request.AddressList)),
                s => s.Terms(k =>
                    k.Field(f => f.ToTransfer.ToAddress).Terms(request.AddressList)))));
        }

        if (request.Type > 0)
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.OrderType)
                    .Value(Enum.GetName(typeof(OrderTypeEnum), request.Type))));
        }

        var status = OrderStatusResponseEnum.All;
        if (request.Status > 0)
        {
            status =
                (OrderStatusResponseEnum)Enum.Parse(typeof(OrderStatusResponseEnum), request.Status.ToString());
            switch (status)
            {
                case OrderStatusResponseEnum.Processing:
                    mustQuery.Add(q => q.Terms(i =>
                        i.Field(f => f.Status).Terms(OrderStatusHelper.GetProcessingList())));
                    mustQuery.Add(q => q.Bool(i => i.Should(
                        s => s.Match(k =>
                            k.Field("extensionInfo.ToConfirmedNum").Query("0")),
                        p => p.Bool(j => j.MustNot(
                            s => s.Exists(k =>
                                k.Field("extensionInfo.ToConfirmedNum")))))));
                    break;
                case OrderStatusResponseEnum.Succeed:
                    mustQuery.Add(q => q.Bool(i => i.Should(
                        s => s.Terms(k =>
                            k.Field(f => f.Status).Terms(OrderStatusHelper.GetSucceedList())),
                        p => p.Bool(j => j.Must(
                            s => s.Exists(k =>
                                k.Field("extensionInfo.ToConfirmedNum")),
                            s => s.Terms(k =>
                                k.Field(f => f.Status).Terms(OrderStatusHelper.GetProcessingList())))))));
                    break;
                case OrderStatusResponseEnum.Failed:
                    mustQuery.Add(q => q.Terms(i =>
                        i.Field(f => f.Status).Terms(OrderStatusHelper.GetFailedList())));
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
        
        var mustNotQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustNotQuery.Add(q => q.Match(i =>
            i.Field("extensionInfo.RefundTx").Query(ExtensionKey.RefundTx)));
        if (status == OrderStatusResponseEnum.Succeed)
        {
            mustNotQuery.Add(q => q.Match(i =>
                i.Field("extensionInfo.ToConfirmedNum").Query("0")));
        }
        mustNotQuery.Add(GetFilterCondition());

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery)
            .MustNot(mustNotQuery));

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
                _objectMapper.Map<List<OrderIndex>, List<OrderIndexDto>>(list), list),
            TotalCount = count
        };
        
        if (orderIndexDtoPageResult.Items.Any())
        {
            var maxCreateTime = orderIndexDtoPageResult.Items.Max(item => item.CreateTime);
            if (userId.HasValue)
            {
                var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(userId.ToString());
                await userOrderActionGrain.AddOrUpdate(maxCreateTime);
            }
            if (!request.AddressList.IsNullOrEmpty())
            {
                foreach (var address in request.AddressList)
                {
                    var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(address);
                    await userOrderActionGrain.AddOrUpdate(maxCreateTime);
                }
            }
        }
        
        return orderIndexDtoPageResult;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderAppService),
        MethodName = nameof(HandleGetDetailExceptionAsync))]
    public async Task<OrderDetailDto> GetOrderRecordDetailAsync(string id)
    {
        if (id.IsNullOrWhiteSpace()) return new OrderDetailDto();
        return (await GetOrderDetailAsync(id)).Item1;
    }

    public async Task<Tuple<OrderDetailDto, OrderIndex>> GetOrderDetailAsync(string id, bool includeAll = false)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.Id).Value(id)));
            
        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

        var orderIndex = await _orderIndexRepository.GetAsync(Filter);
        return Tuple.Create(await GetOrderDetailDtoAsync(orderIndex, includeAll), orderIndex);
    }

    public async Task<OrderIndexDto> GetTransferOrderAsync(CoBoTransactionDto coBoTransaction)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Withdraw.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.OrderType").Value(OrderTypeEnum.Transfer.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.SubStatus").Value(OrderOperationStatusEnum.UserTransferRejected.ToString())));
        mustQuery.Add(q => q.Bool(i => i.Should(
            s => s.Term(w =>
                w.Field(f => f.ThirdPartOrderId).Value(coBoTransaction.Id)),
            s => s.Term(w =>
                w.Field(f => f.FromTransfer.TxId).Value(coBoTransaction.TxId)))));
        
        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var index = await _orderIndexRepository.GetAsync(Filter);
        if (index != null) return _objectMapper.Map<OrderIndex, OrderIndexDto>(index);
        
        var coin = coBoTransaction.Coin.Split(CommonConstant.Underline);
        if (coin.Length < 2) return null;
        mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Withdraw.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.OrderType").Value(OrderTypeEnum.Transfer.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.SubStatus").Value(OrderOperationStatusEnum.UserTransferRejected.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.FromAddress).Value(coBoTransaction.SourceAddress).CaseInsensitive()));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.ToAddress).Value(coBoTransaction.Address).CaseInsensitive()));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Amount).Value(coBoTransaction.Amount.SafeToDecimal())));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Network).Value(coin[0])));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Symbol).Value(coin[1])));

        var mustNotQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustNotQuery.Add(q => q.Exists(i => i.Field(t => t.ThirdPartOrderId)));

        QueryContainer Filter2(QueryContainerDescriptor<OrderIndex> f) =>
            f.Bool(b => b.Must(mustQuery).MustNot(mustNotQuery));
        
        var (count, list) = await _orderIndexRepository.GetSortListAsync(Filter2,
            sortFunc: s => s.Descending(t => t.CreateTime),
            limit: 1);
        if (count == 0) return null;
        return _objectMapper.Map<OrderIndex, OrderIndexDto>(list[0]);
    }

    public async Task<bool> CheckTransferOrderAsync(CoBoTransactionDto coBoTransaction, long time)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Withdraw.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.OrderType").Value(OrderTypeEnum.Transfer.ToString())));
        mustQuery.Add(q => q.Bool(i => i.Should(
            s => s.Term(w =>
                w.Field(f => f.ThirdPartOrderId).Value(coBoTransaction.Id)),
            s => s.Term(w =>
                w.Field(f => f.FromTransfer.TxId).Value(coBoTransaction.TxId)))));

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) =>
            f.Bool(b => b.Must(mustQuery));

        var count = await _orderIndexRepository.CountAsync(Filter);
        if (count.Count > 0) return true;
        if (DateTime.UtcNow.ToUtcMilliSeconds() - time >= OrderOptions.SubMilliSeconds) return false;

        var coin = coBoTransaction.Coin.Split(CommonConstant.Underline);
        if (coin.Length < 2) return false;
        mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.FromAddress).Value(coBoTransaction.SourceAddress).CaseInsensitive()));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.ToAddress).Value(coBoTransaction.Address).CaseInsensitive()));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Amount).Value(coBoTransaction.Amount.SafeToDecimal())));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Network).Value(coin[0])));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Symbol).Value(coin[1])));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Withdraw.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.OrderType").Value(OrderTypeEnum.Transfer.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field("extensionInfo.SubStatus").Value(OrderOperationStatusEnum.UserTransferRejected.ToString())));
        mustQuery.Add(q => q.Range(i =>
            i.Field(f => f.CreateTime).LessThan(time)));
        mustQuery.Add(q => q.Range(i =>
            i.Field(f => f.CreateTime).GreaterThan(time - OrderOptions.SubMilliSeconds)));

        var mustNotQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustNotQuery.Add(q => q.Exists(i => i.Field(t => t.ThirdPartOrderId)));

        QueryContainer Filter2(QueryContainerDescriptor<OrderIndex> f) =>
            f.Bool(b => b.Must(mustQuery).MustNot(mustNotQuery));
        
        count = await _orderIndexRepository.CountAsync(Filter2);
        return count.Count > 0;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderAppService),
        MethodName = nameof(HandleGetUserListExceptionAsync))]
    public async Task<UserOrderDto> GetUserOrderRecordListAsync(GetUserOrderRecordRequestDto request, OrderChangeEto orderEto = null)
    {
        var userOrderDto = new UserOrderDto
        {
            Address = request.Address,
            AddressList = request.AddressList
        };
        UserDto userDto = null;
        if (!request.Address.IsNullOrEmpty())
        {
            var preQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>();
            preQuery.Add(q => q.Term(i => i.Field("addressInfos.address").Value(request.Address)));

            QueryContainer PreFilter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(preQuery));
            var user = await _userIndexRepository.GetListAsync(PreFilter);
            userDto = ObjectMapper.Map<UserIndex, UserDto>(user.Item2.FirstOrDefault());
            if ((userDto == null || userDto.UserId == Guid.Empty) && request.AddressList.IsNullOrEmpty())
            {
                return userOrderDto;
            }
        }
        
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        if (userDto != null && request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userDto.UserId.ToString())));
        }
        else if (!request.AddressList.IsNullOrEmpty())
        {
            var addressList = request.AddressList.ConvertAll(t => t.Address).ToList();
            if (userDto != null)
            {
                mustQuery.Add(q => q.Bool(i => i.Should(
                    s => s.Term(k =>
                        k.Field(f => f.UserId).Value(userDto.UserId.ToString())),
                    s => s.Terms(k =>
                        k.Field(f => f.FromTransfer.FromAddress).Terms(addressList)),
                    s => s.Terms(k =>
                        k.Field(f => f.ToTransfer.ToAddress).Terms(addressList)))));
            }
            else
            {
                mustQuery.Add(q => q.Bool(i => i.Should(
                    s => s.Terms(k =>
                        k.Field(f => f.FromTransfer.FromAddress).Terms(addressList)),
                    s => s.Terms(k =>
                        k.Field(f => f.ToTransfer.ToAddress).Terms(addressList)))));
            }
        }

        if (request.Time.HasValue && request.Time.Value > 0)
        {
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime)
                    .GreaterThanOrEquals(DateTime.UtcNow.AddHours(-1 * request.Time.Value).ToUtcMilliSeconds())));
        }
        else
        {
            mustQuery.Add(q => q.Range(i =>
                i.Field(f => f.CreateTime).GreaterThanOrEquals(DateTime.UtcNow
                    .AddDays(OrderOptions.ValidOrderMessageThreshold).ToUtcMilliSeconds())));
        }

        var mustNotQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustNotQuery.Add(GetFilterCondition());
        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery)
            .MustNot(mustNotQuery));

        var (count, list) = await _orderIndexRepository.GetSortListAsync(Filter,
            sortFunc: s => s.Descending(t => t.CreateTime));

        if (orderEto != null)
        {
            var orderFirst = list.FirstOrDefault(item => item.Id == orderEto.Id);
            if (orderFirst != null)
            {
                list.Remove(orderFirst);
            }
            list.Add(_objectMapper.Map<OrderChangeEto, OrderIndex>(orderEto));
        }

        return await LoopCollectionRecordsAsync(list, userOrderDto);
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(OrderAppService),
        MethodName = nameof(HandleGetStatusExceptionAsync))]
    public async Task<OrderStatusDto> GetOrderRecordStatusAsync(GetOrderRecordStatusRequestDto request)
    {
        var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
        if ((!userId.HasValue || userId == Guid.Empty) && request.AddressList.IsNullOrEmpty())
        {
            return new OrderStatusDto();
        }

        var lastModifyTime = 0L;
        if (userId.HasValue)
        {
            var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(userId.ToString());
            lastModifyTime = Math.Max(lastModifyTime, await userOrderActionGrain.Get());
        }
        if (!request.AddressList.IsNullOrEmpty())
        {
            foreach (var address in request.AddressList)
            {
                var userOrderActionGrain = _clusterClient.GetGrain<IUserOrderActionGrain>(address);
                lastModifyTime = Math.Max(lastModifyTime, await userOrderActionGrain.Get());
            }
        }

        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>
        {
            q => q.Range(i => i.Field(f => f.CreateTime).GreaterThan(lastModifyTime)),
            q => q.Range(i => i.Field(f => f.CreateTime).GreaterThan(DateTime.UtcNow.AddDays(OrderOptions.ValidOrderThreshold).ToUtcMilliSeconds())),
            q => q.Terms(i => i.Field(f => f.Status).Terms((IEnumerable<string>)OrderStatusHelper.GetProcessingList()))
        };
        if (userId.HasValue && request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userId.ToString())));
        }
        else if (userId.HasValue && !request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Term(k =>
                    k.Field(f => f.UserId).Value(userId.ToString())),
                s => s.Terms(k =>
                    k.Field(f => f.FromTransfer.FromAddress).Terms(request.AddressList)),
                s => s.Terms(k =>
                    k.Field(f => f.ToTransfer.ToAddress).Terms(request.AddressList)))));
        }
        else if (!request.AddressList.IsNullOrEmpty())
        {
            mustQuery.Add(q => q.Bool(i => i.Should(
                s => s.Terms(k =>
                    k.Field(f => f.FromTransfer.FromAddress).Terms(request.AddressList)),
                s => s.Terms(k =>
                    k.Field(f => f.ToTransfer.ToAddress).Terms(request.AddressList)))));
        }

        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));
        
        var count = await _orderIndexRepository.CountAsync(Filter);
        
        return new OrderStatusDto
        {
            Status = count.Count > 0
        };
    }

    private static Func<QueryContainerDescriptor<OrderIndex>, QueryContainer> GetFilterCondition()
    {
        QueryContainer query(QueryContainerDescriptor<OrderIndex> q) => q.Bool(i => i.Must(
            s => s.Term(k =>
                k.Field("extensionInfo.OrderType").Value(OrderTypeEnum.Transfer.ToString())),
            p => p.Bool(j => j.Should(
                s => s.Term(k =>
                    k.Field("extensionInfo.SubStatus").Value(OrderOperationStatusEnum.UserTransferRejected.ToString())),
                q => q.Bool(b => b.MustNot(
                    s => s.Exists(k =>
                        k.Field(f => f.FromTransfer.TxId)))),
                q => q.Bool(b => b.Must(
                    s => s.Range(k =>
                        k.Field(f => f.CreateTime).LessThan(DateTime.UtcNow.AddHours(-48).ToUtcMilliSeconds())),
                    s => s.Exists(k =>
                        k.Field(f => f.FromTransfer.TxId)),
                    r => r.Bool(d => d.MustNot(
                        s => s.Exists(k =>
                            k.Field(f => f.ThirdPartOrderId)))),
                    r => r.Bool(d => d.MustNot(
                        s => s.Term(k =>
                            k.Field("extensionInfo.SubStatus")
                                .Value(OrderOperationStatusEnum.UserTransferRejected.ToString()))))))))));

        return query;
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

    private async Task<List<OrderIndexDto>> LoopCollectionItemsAsync(List<OrderIndexDto> itemList, List<OrderIndex> list)
    {
        foreach (var item in itemList)
        {
            var index = list.FirstOrDefault(t => t.Id == item.Id);
            await HandleItemAsync(item, index);
        }

        return itemList;
    }

    private async Task<OrderIndexDto> HandleItemAsync(OrderIndexDto item, OrderIndex orderIndex, bool includeAll = false)
    {
        var status = Enum.Parse<OrderStatusEnum>(item.Status);
        switch (status)
        {
            case OrderStatusEnum.ToTransferConfirmed:
            case OrderStatusEnum.Finish:
                item.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ToTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ArrivalTime = item.LastModifyTime;
                break;
            case OrderStatusEnum.FromTransferFailed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Failed.ToString();
                item.ToTransfer.Status = string.Empty;
                item.ArrivalTime = 0;
                break;
            case OrderStatusEnum.ToTransferFailed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                item.ToTransfer.Status = OrderStatusResponseEnum.Failed.ToString();
                item.ArrivalTime = 0;
                break;
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Failed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status, status.ToString());
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status, status.ToString());
                item.ArrivalTime = 0;
                break;
            default:
                item.Status = OrderStatusResponseEnum.Processing.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status);
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status);
                if (!includeAll && item.FromTransfer.Status == OrderStatusResponseEnum.Succeed.ToString()
                                && item.ToTransfer.Status == OrderStatusResponseEnum.Processing.ToString()
                                && orderIndex != null && !orderIndex.ExtensionInfo.IsNullOrEmpty()
                                && orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.ToConfirmedNum)
                                && int.TryParse(orderIndex.ExtensionInfo[ExtensionKey.ToConfirmedNum],
                                    out var confirmedNum) && confirmedNum > 0)
                {
                    item.Status = OrderStatusResponseEnum.Succeed.ToString();
                    item.ToTransfer.Status = OrderStatusResponseEnum.Succeed.ToString();
                }

                break;
        }

        item.FromTransfer.Amount = item.FromTransfer.Amount.SafeToDecimal(0M).ToString(
            await _networkAppService.GetDecimalsAsync(ChainId.AELF, item.FromTransfer.Symbol),
            DecimalHelper.RoundingOption.Floor);
        item.ToTransfer.Amount = item.ToTransfer.Amount.SafeToDecimal(0M).ToString(
            await _networkAppService.GetDecimalsAsync(ChainId.AELF, item.ToTransfer.Symbol),
            DecimalHelper.RoundingOption.Floor);
        item.FromTransfer.AmountUsd =
            (item.FromTransfer.Amount.SafeToDecimal(0M) * await GetExchangeAsync(item.FromTransfer.Symbol))
            .ToString(2, DecimalHelper.RoundingOption.Floor);
        item.ToTransfer.AmountUsd =
            (item.ToTransfer.Amount.SafeToDecimal(0M) * await GetExchangeAsync(item.ToTransfer.Symbol))
            .ToString(2, DecimalHelper.RoundingOption.Floor);
        item.FromTransfer.Icon =
            await _networkAppService.GetIconAsync(item.OrderType, ChainId.AELF, item.FromTransfer.Symbol);
        item.ToTransfer.Icon =
            await _networkAppService.GetIconAsync(item.OrderType, ChainId.AELF, item.FromTransfer.Symbol, item.ToTransfer.Symbol);
        item.SecondOrderType = orderIndex != null && !orderIndex.ExtensionInfo.IsNullOrEmpty() &&
                               orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.OrderType)
            ? orderIndex.ExtensionInfo[ExtensionKey.OrderType]
            : string.Empty;
        return item;
    }

    private async Task<decimal> GetExchangeAsync(string symbol)
    {
        var avgExchange = 0M;
        try
        {
            avgExchange =
                await _networkAppService.GetAvgExchangeAsync(symbol, CommonConstant.Symbol.USD);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "HandleItemAsync exchange error, {symbol}", symbol);
        }

        return avgExchange;
    }

    private string GetTransferStatus(string transferStatus, string orderStatus = null)
    {
        if(transferStatus == CommonConstant.SuccessStatus) return OrderStatusResponseEnum.Succeed.ToString();
        if(transferStatus == CommonConstant.PendingStatus) return OrderStatusResponseEnum.Processing.ToString();
        try
        {
            var status = Enum.Parse<OrderTransferStatusEnum>(transferStatus);
            switch (status)
            {
                case OrderTransferStatusEnum.Confirmed:
                    return OrderStatusResponseEnum.Succeed.ToString();
                case OrderTransferStatusEnum.TransferFailed:
                case OrderTransferStatusEnum.Failed:
                    return OrderStatusResponseEnum.Failed.ToString();
                default:
                    if (orderStatus == OrderStatusEnum.Expired.ToString() 
                        || orderStatus == OrderStatusEnum.Failed.ToString())
                        return OrderStatusResponseEnum.Failed.ToString();
                    return OrderStatusResponseEnum.Processing.ToString();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "OrderTransferStatusEnum parse error, status={status}", transferStatus);
            if (orderStatus == OrderStatusEnum.Expired.ToString() || orderStatus == OrderStatusEnum.Failed.ToString())
                return OrderStatusResponseEnum.Failed.ToString();
            if (transferStatus.IsNullOrEmpty()) return string.Empty;
            return OrderStatusResponseEnum.Processing.ToString();
        }
    }

    private async Task<OrderDetailDto> GetOrderDetailDtoAsync(OrderIndex orderIndex, bool includeAll = false)
    {
        var orderIndexDto = await HandleItemAsync(_objectMapper.Map<OrderIndex, OrderIndexDto>(orderIndex), orderIndex, includeAll);
        var detailDto = _objectMapper.Map<OrderIndexDto, OrderDetailDto>(orderIndexDto);

        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.FromConfirmingThreshold))
            detailDto.Step.FromTransfer.ConfirmingThreshold =
                int.Parse(orderIndex.ExtensionInfo[ExtensionKey.FromConfirmingThreshold]);
        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.FromConfirmedNum))
            detailDto.Step.FromTransfer.ConfirmedNum =
                int.Parse(orderIndex.ExtensionInfo[ExtensionKey.FromConfirmedNum]);
        if (orderIndex.OrderType == OrderTypeEnum.Deposit.ToString() &&
            detailDto.Step.FromTransfer.ConfirmingThreshold == 0)
        {
            if (orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Confirmed.ToString()
                || orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Transferring.ToString()
                || orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Transferred.ToString())
            {
                var coBoCoinGrain =
                    _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(orderIndex.FromTransfer.Network,
                        orderIndex.FromTransfer.Symbol));
                detailDto.Step.FromTransfer.ConfirmingThreshold = await coBoCoinGrain.GetConfirmingThreshold();
            }
        }
        else if (orderIndex.OrderType == OrderTypeEnum.Withdraw.ToString() &&
                 detailDto.Step.FromTransfer.ConfirmingThreshold == 0)
        {
            if (orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Confirmed.ToString()
                || orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Transferring.ToString()
                || orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Transferred.ToString())
            {
                var coBoCoinGrain =
                    _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(orderIndex.ToTransfer.Network,
                        orderIndex.FromTransfer.Symbol));
                detailDto.Step.FromTransfer.ConfirmingThreshold =
                    await coBoCoinGrain.GetHomogeneousConfirmingThreshold(orderIndex.FromTransfer.Amount);
            }
        }

        if (orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Confirmed.ToString()
            && detailDto.Step.FromTransfer.ConfirmedNum < detailDto.Step.FromTransfer.ConfirmingThreshold)
        {
            detailDto.Step.FromTransfer.ConfirmedNum = detailDto.Step.FromTransfer.ConfirmingThreshold;
        }

        if (detailDto.Step.FromTransfer.ConfirmedNum > detailDto.Step.FromTransfer.ConfirmingThreshold)
            detailDto.Step.FromTransfer.ConfirmedNum = detailDto.Step.FromTransfer.ConfirmingThreshold;

        if (detailDto.Status == OrderStatusResponseEnum.Succeed.ToString()
            || detailDto.Status == OrderStatusResponseEnum.Failed.ToString())
            detailDto.Step.CurrentStep = 3;
        else if (detailDto.Status == OrderStatusResponseEnum.Processing.ToString())
        {
            if (orderIndex.FromTransfer.Status == OrderTransferStatusEnum.Created.ToString()
                || orderIndex.FromTransfer.Status == OrderTransferStatusEnum.StartTransfer.ToString())
            {
                detailDto.Step.CurrentStep = 0;
            }
            else if (detailDto.FromTransfer.Status == OrderStatusResponseEnum.Processing.ToString())
            {
                detailDto.Step.CurrentStep = 1;
            }
            else if (detailDto.ToTransfer.Status.IsNullOrEmpty() ||
                     detailDto.ToTransfer.Status == OrderStatusResponseEnum.Processing.ToString())
            {
                detailDto.Step.CurrentStep = 2;
            }
        }

        return detailDto;
    }

    private async Task<UserOrderDto> LoopCollectionRecordsAsync(List<OrderIndex> itemList, UserOrderDto dto)
    {
        dto.Processing.Deposit =
            await GetUserDepositOrderInfoListAsync(itemList, OrderStatusResponseEnum.Processing, OrderTypeEnum.Deposit);
        dto.Processing.DepositCount = dto.Processing.Deposit.Count;
        dto.Processing.Transfer =
            await GetUserTransferOrderInfoListAsync(itemList, OrderStatusResponseEnum.Processing, OrderTypeEnum.Withdraw);
        dto.Processing.TransferCount = dto.Processing.Transfer.Count;
        dto.Processing.Withdraw = dto.Processing.Transfer;
        dto.Processing.WithdrawCount = dto.Processing.TransferCount;

        dto.Succeed.Deposit =
            await GetUserDepositOrderInfoListAsync(itemList, OrderStatusResponseEnum.Succeed, OrderTypeEnum.Deposit);
        dto.Succeed.DepositCount = dto.Succeed.Deposit.Count;
        dto.Succeed.Transfer =
            await GetUserTransferOrderInfoListAsync(itemList, OrderStatusResponseEnum.Succeed, OrderTypeEnum.Withdraw);
        dto.Succeed.TransferCount = dto.Succeed.Transfer.Count;
        dto.Succeed.Withdraw = dto.Succeed.Transfer;
        dto.Succeed.WithdrawCount = dto.Succeed.TransferCount;

        dto.Failed.Deposit =
            await GetUserDepositOrderInfoListAsync(itemList, OrderStatusResponseEnum.Failed, OrderTypeEnum.Deposit);
        dto.Failed.DepositCount = dto.Failed.Deposit.Count;
        dto.Failed.Transfer =
            await GetUserTransferOrderInfoListAsync(itemList, OrderStatusResponseEnum.Failed, OrderTypeEnum.Withdraw);
        dto.Failed.TransferCount = dto.Failed.Transfer.Count;
        dto.Failed.Withdraw = dto.Failed.Transfer;
        dto.Failed.WithdrawCount = dto.Failed.TransferCount;

        return dto;
    }

    private async Task<List<UserDepositOrderInfo>> GetUserDepositOrderInfoListAsync(List<OrderIndex> itemList,
        OrderStatusResponseEnum orderStatus, OrderTypeEnum orderType)
    {
        var result = new List<UserDepositOrderInfo>();
        var recordList = await FilterOrderIndexListAsync(itemList, orderStatus, orderType);
        
        foreach (var item in recordList)
        {
            var record = new UserDepositOrderInfo
            {
                Id = item.Id.ToString(),
                Symbol = item.ToTransfer.Symbol,
                Amount = item.ToTransfer.Amount.ToString(
                    await _networkAppService.GetDecimalsAsync(ChainId.AELF, item.ToTransfer.Symbol),
                    DecimalHelper.RoundingOption.Floor),
                IsSwap = !item.ExtensionInfo.IsNullOrEmpty() &&
                         item.ExtensionInfo.ContainsKey(ExtensionKey.SwapStage),
                IsSwapFail =
                    !item.ExtensionInfo.IsNullOrEmpty() && item.ExtensionInfo.ContainsKey(ExtensionKey.SwapStage)
                                                        && item.ExtensionInfo[ExtensionKey.SwapStage] !=
                                                        SwapStage.SwapTx
            };
            result.Add(record);
        }

        return result;
    }
    
    private async Task<List<UserTransferOrderInfo>> GetUserTransferOrderInfoListAsync(List<OrderIndex> itemList,
        OrderStatusResponseEnum orderStatus, OrderTypeEnum orderType)
    {
        var result = new List<UserTransferOrderInfo>();
        var recordList = await FilterOrderIndexListAsync(itemList, orderStatus, orderType);
        
        foreach (var item in recordList)
        {
            var record = new UserTransferOrderInfo
            {
                Id = item.Id.ToString(),
                Symbol = item.ToTransfer.Symbol,
                Amount = item.ToTransfer.Amount.ToString(
                    await _networkAppService.GetDecimalsAsync(ChainId.AELF, item.ToTransfer.Symbol),
                    DecimalHelper.RoundingOption.Floor)
            };
            result.Add(record);
        }

        return result;
    }
    
    private async Task<List<OrderIndex>> FilterOrderIndexListAsync(List<OrderIndex> itemList,
        OrderStatusResponseEnum orderStatus, OrderTypeEnum orderType)
    {
        var statusList = new List<string>();
        switch (orderStatus)
        {
            case OrderStatusResponseEnum.Processing:
                statusList = OrderStatusHelper.GetProcessingList();
                break;
            case OrderStatusResponseEnum.Succeed:
                statusList = OrderStatusHelper.GetSucceedList();
                break;
            case OrderStatusResponseEnum.Failed:
                statusList = OrderStatusHelper.GetFailedList();
                break;
        }

        return itemList.Where(i => i.OrderType == orderType.ToString() && statusList.Contains(i.Status))
            .ToList();
    }
}