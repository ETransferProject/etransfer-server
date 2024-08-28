using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Entities;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Network;
using ETransferServer.Options;
using ETransferServer.Orders;
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
public class OrderAppService : ApplicationService, IOrderAppService
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
                            i.Field(f => f.Status).Terms(OrderStatusHelper.GetProcessingList())));
                        break;
                    case OrderStatusResponseEnum.Succeed:
                        mustQuery.Add(q => q.Terms(i =>
                            i.Field(f => f.Status).Terms(OrderStatusHelper.GetSucceedList())));
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

    public async Task<OrderDetailDto> GetOrderRecordDetailAsync(string id)
    {
        try
        {
            var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
            if (id.IsNullOrWhiteSpace() || !userId.HasValue || userId == Guid.Empty) return new OrderDetailDto();
            
            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userId.ToString())));
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.Id).Value(id)));
            
            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

            var orderIndex = await _orderIndexRepository.GetAsync(Filter);
            return await GetOrderDetailDtoAsync(orderIndex);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get order record detail failed, orderId={id}", id);
            return new OrderDetailDto();
        }
    }

    public async Task<UserOrderDto> GetUserOrderRecordListAsync(GetUserOrderRecordRequestDto request)
    {
        var userOrderDto = new UserOrderDto
        {
            Address = request.Address
        };
        try
        {
            var preQuery = new List<Func<QueryContainerDescriptor<UserIndex>, QueryContainer>>();
            preQuery.Add(q => q.Term(i => i.Field("addressInfos.address").Value(request.Address)));

            QueryContainer PreFilter(QueryContainerDescriptor<UserIndex> f) => f.Bool(b => b.Must(preQuery));
            var user = await _userIndexRepository.GetListAsync(PreFilter);
            var userDto = ObjectMapper.Map<UserIndex, UserDto>(user.Item2.FirstOrDefault());
            if (userDto == null || userDto.UserId == Guid.Empty)
            {
                return userOrderDto;
            }

            var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.UserId).Value(userDto.UserId.ToString())));

            if (request.MinTimestamp.HasValue && request.MinTimestamp.Value > 0)
            {
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.CreateTime).GreaterThanOrEquals(request.MinTimestamp.Value)));
            }
            else
            {
                mustQuery.Add(q => q.Range(i =>
                    i.Field(f => f.CreateTime).GreaterThanOrEquals(DateTime.UtcNow
                        .AddDays(OrderOptions.ValidOrderMessageThreshold).ToUtcMilliSeconds())));
            }

            QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

            var (count, list) = await _orderIndexRepository.GetSortListAsync(Filter,
                sortFunc: s => s.Descending(t => t.CreateTime));

            return await LoopCollectionRecordsAsync(list, userOrderDto);

        }
        catch (Exception e)
        {
            _logger.LogError(e, "Get user order record list failed, address={Address}, minTimestamp=" +
                                "{MinTimestamp}", request.Address, request.MinTimestamp);
            return userOrderDto;
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
                q => q.Terms(i => i.Field(f => f.Status).Terms((IEnumerable<string>)OrderStatusHelper.GetProcessingList()))
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
        foreach (var item in itemList)
        {
            await HandleItemAsync(item);
        }

        return itemList;
    }

    private async Task<OrderIndexDto> HandleItemAsync(OrderIndexDto item)
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
            case OrderStatusEnum.ToTransferFailed:
            case OrderStatusEnum.Expired:
            case OrderStatusEnum.Failed:
                item.Status = OrderStatusResponseEnum.Failed.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status);
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status);
                item.ArrivalTime = 0;
                break;
            default:
                item.Status = OrderStatusResponseEnum.Processing.ToString();
                item.FromTransfer.Status = GetTransferStatus(item.FromTransfer.Status);
                item.ToTransfer.Status = GetTransferStatus(item.ToTransfer.Status);
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
            await _networkAppService.GetIconAsync(item.OrderType, ChainId.AELF, item.ToTransfer.Symbol);
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

    private string GetTransferStatus(string transferStatus)
    {
        if(transferStatus == CommonConstant.SuccessStatus) return OrderStatusResponseEnum.Succeed.ToString();
        if(transferStatus.IsNullOrEmpty()) return OrderStatusResponseEnum.Processing.ToString();
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
                    return OrderStatusResponseEnum.Processing.ToString();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "OrderTransferStatusEnum parse error, status={status}", transferStatus);
            return OrderStatusResponseEnum.Processing.ToString();
        }
    }

    private async Task<OrderDetailDto> GetOrderDetailDtoAsync(OrderIndex orderIndex)
    {
        var orderIndexDto = await HandleItemAsync(_objectMapper.Map<OrderIndex, OrderIndexDto>(orderIndex));
        var detailDto = _objectMapper.Map<OrderIndexDto, OrderDetailDto>(orderIndexDto);

        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.FromConfirmingThreshold))
            detailDto.Step.FromTransfer.ConfirmingThreshold =
                int.Parse(orderIndex.ExtensionInfo[ExtensionKey.FromConfirmingThreshold]);
        if (!orderIndex.ExtensionInfo.IsNullOrEmpty() &&
            orderIndex.ExtensionInfo.ContainsKey(ExtensionKey.FromConfirmedNum))
            detailDto.Step.FromTransfer.ConfirmedNum =
                int.Parse(orderIndex.ExtensionInfo[ExtensionKey.FromConfirmedNum]);
        if (orderIndex.OrderType == OrderTypeEnum.Deposit.ToString() && detailDto.Step.FromTransfer.ConfirmingThreshold == 0)
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
                    _clusterClient.GetGrain<ICoBoCoinGrain>(ICoBoCoinGrain.Id(orderIndex.FromTransfer.Network,
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
            else if (detailDto.ToTransfer.Status == OrderStatusResponseEnum.Processing.ToString())
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
        dto.Processing.Withdraw =
            await GetUserWithdrawOrderInfoListAsync(itemList, OrderStatusResponseEnum.Processing, OrderTypeEnum.Withdraw);
        dto.Processing.WithdrawCount = dto.Processing.Withdraw.Count;

        dto.Succeed.Deposit =
            await GetUserDepositOrderInfoListAsync(itemList, OrderStatusResponseEnum.Succeed, OrderTypeEnum.Deposit);
        dto.Succeed.DepositCount = dto.Succeed.Deposit.Count;
        dto.Succeed.Withdraw =
            await GetUserWithdrawOrderInfoListAsync(itemList, OrderStatusResponseEnum.Succeed, OrderTypeEnum.Withdraw);
        dto.Succeed.WithdrawCount = dto.Succeed.Withdraw.Count;

        dto.Failed.Deposit =
            await GetUserDepositOrderInfoListAsync(itemList, OrderStatusResponseEnum.Failed, OrderTypeEnum.Deposit);
        dto.Failed.DepositCount = dto.Failed.Deposit.Count;
        dto.Failed.Withdraw =
            await GetUserWithdrawOrderInfoListAsync(itemList, OrderStatusResponseEnum.Failed, OrderTypeEnum.Withdraw);
        dto.Failed.WithdrawCount = dto.Failed.Withdraw.Count;

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
    
    private async Task<List<UserWithdrawOrderInfo>> GetUserWithdrawOrderInfoListAsync(List<OrderIndex> itemList,
        OrderStatusResponseEnum orderStatus, OrderTypeEnum orderType)
    {
        var result = new List<UserWithdrawOrderInfo>();
        var recordList = await FilterOrderIndexListAsync(itemList, orderStatus, orderType);
        
        foreach (var item in recordList)
        {
            var record = new UserWithdrawOrderInfo
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