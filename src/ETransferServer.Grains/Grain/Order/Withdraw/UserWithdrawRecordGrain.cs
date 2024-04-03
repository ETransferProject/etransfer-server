using Microsoft.Extensions.Logging;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
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
    private readonly IOptionsMonitor<ChainOptions> _chainOptions;
    private readonly IOptionsMonitor<WithdrawNetworkOptions> _withdrawNetworkOptions;

    public UserWithdrawRecordGrain(IObjectMapper objectMapper, 
        ILogger<UserWithdrawRecordGrain> logger,
        IOptionsMonitor<ChainOptions> chainOptions,
        IOptionsMonitor<WithdrawNetworkOptions> withdrawNetworkOptions)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _chainOptions = chainOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
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
                var netWorkInfo = _withdrawNetworkOptions.CurrentValue.NetworkInfos.FirstOrDefault(t =>
                    t.Coin.Equals(GuidHelper.GenerateId(orderDto.ToTransfer.Network, 
                        orderDto.ToTransfer.Symbol), StringComparison.OrdinalIgnoreCase));
                State.ArrivalTime = DateTime.UtcNow.AddSeconds(
                        _chainOptions.CurrentValue.ChainInfos[orderDto.FromTransfer.ChainId].EstimatedArrivalTime)
                    .AddSeconds(netWorkInfo.EstimatedArrivalTime).ToUtcMilliSeconds();
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
}