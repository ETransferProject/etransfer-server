using Microsoft.Extensions.Logging;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IUserWithdrawRecordGrain : IGrainWithGuidKey
{
    Task<CommonResponseDto<WithdrawOrderDto>> AddOrUpdateAsync(WithdrawOrderDto orderDto);


    Task<CommonResponseDto<WithdrawOrderDto>> GetAsync();
}

public class UserWithdrawRecordGrain : Grain<WithdrawOrderState>, IUserWithdrawRecordGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UserWithdrawRecordGrain> _logger;
    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;
    private readonly IOptionsSnapshot<WithdrawOptions> _withdrawOptions;

    public UserWithdrawRecordGrain(IObjectMapper objectMapper, 
        ILogger<UserWithdrawRecordGrain> logger,
        IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions,
        IOptionsSnapshot<WithdrawOptions> withdrawOptions)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _chainOptions = chainOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
        _withdrawOptions = withdrawOptions;
    }

    public async Task<CommonResponseDto<WithdrawOrderDto>> AddOrUpdateAsync(WithdrawOrderDto orderDto)
    {
        try
        {
            _objectMapper.Map(orderDto, State);
            var now = DateTime.UtcNow.ToUtcMilliSeconds();
            var createTime = State.CreateTime ?? DateTime.UtcNow.ToUtcMilliSeconds();

            State.Id = this.GetPrimaryKey();
            State.CreateTime = createTime;
            State.LastModifyTime = now;
            if (!State.ArrivalTime.HasValue)
            {
                var netWorkInfo = _withdrawNetworkOptions.Value.NetworkInfos.FirstOrDefault(t =>
                    t.Coin.Equals(GuidHelper.GenerateId(orderDto.ToTransfer.Network,
                        orderDto.ToTransfer.Symbol), StringComparison.OrdinalIgnoreCase));

                State.ArrivalTime = !IsBigAmountInAElf(orderDto).HasValue
                    ? DateTime.UtcNow.AddSeconds(
                            _chainOptions.Value.ChainInfos[orderDto.FromTransfer.ChainId].EstimatedArrivalTime)
                        .AddSeconds(netWorkInfo.EstimatedArrivalTime).ToUtcMilliSeconds()
                    : IsBigAmountInAElf(orderDto).Value
                        ? DateTime.UtcNow.AddSeconds(
                                _chainOptions.Value.ChainInfos[orderDto.FromTransfer.ChainId]
                                    .EstimatedArrivalFastUpperTime * 2)
                            .ToUtcMilliSeconds()
                        : DateTime.UtcNow.AddSeconds(
                                _chainOptions.Value.ChainInfos[orderDto.FromTransfer.ChainId]
                                    .EstimatedArrivalFastLowerTime * 2)
                            .ToUtcMilliSeconds();
            }
            if (orderDto.Status == OrderStatusEnum.Finish.ToString())
            {
                State.ArrivalTime = State.LastModifyTime;
            }

            await WriteStateAsync();
            
            return new CommonResponseDto<WithdrawOrderDto>(_objectMapper.Map<WithdrawOrderState, WithdrawOrderDto>(State));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Withdraw order grain create error");
            return new CommonResponseDto<WithdrawOrderDto>().Error(e);
        }
    }


    public Task<CommonResponseDto<WithdrawOrderDto>> GetAsync()
    {
        return State.Id == Guid.Empty
            ? new Task<CommonResponseDto<WithdrawOrderDto>>(null!)
            : Task.FromResult(
                new CommonResponseDto<WithdrawOrderDto>(_objectMapper.Map<WithdrawOrderState, WithdrawOrderDto>(State)));
    }

    private bool? IsBigAmountInAElf(WithdrawOrderDto orderDto)
    {
        try
        {
            if (orderDto.ToTransfer.Network != CommonConstant.Network.AElf) return null;
            var symbol = orderDto.FromTransfer.Symbol;
            var thresholdExists = _withdrawOptions.Value.Homogeneous.TryGetValue(symbol, out var threshold);
            AssertHelper.IsTrue(thresholdExists, "Homogeneous symbol {} not found", symbol);
            AssertHelper.NotNull(threshold, "Homogeneous threshold not fount, symbol:{}", symbol);

            return orderDto.FromTransfer.Amount > threshold.AmountThreshold;
        }
        catch (UserFriendlyException e)
        {
            _logger.LogWarning("UserWithdrawRecordGrain IsBigAmount error, OrderId={OrderId} Message={Msg}", 
                orderDto.Id, e.Message);
            return null;
        }
    }
}