using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
using ETransferServer.Options;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface IUserDepositRecordGrain : IGrainWithGuidKey
{
    Task<CommonResponseDto<DepositOrderDto>> CreateOrUpdateAsync(DepositOrderDto orderDto);

    Task<CommonResponseDto<DepositOrderDto>> GetAsync();
}

public class UserDepositRecordGrain : Grain<DepositOrderState>, IUserDepositRecordGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<UserDepositRecordGrain> _logger;
    private readonly IOptionsSnapshot<ChainOptions> _chainOptions;
    private readonly IOptionsSnapshot<WithdrawNetworkOptions> _withdrawNetworkOptions;

    public UserDepositRecordGrain(IObjectMapper objectMapper, 
        ILogger<UserDepositRecordGrain> logger, 
        IOptionsSnapshot<ChainOptions> chainOptions,
        IOptionsSnapshot<WithdrawNetworkOptions> withdrawNetworkOptions)
    {
        _objectMapper = objectMapper;
        _logger = logger;
        _chainOptions = chainOptions;
        _withdrawNetworkOptions = withdrawNetworkOptions;
    }

    public async Task<CommonResponseDto<DepositOrderDto>> CreateOrUpdateAsync(DepositOrderDto orderDto)
    {
        try
        {
            var now = DateTime.UtcNow.ToUtcMilliSeconds();
            var createTime = State.CreateTime ?? DateTime.UtcNow.ToUtcMilliSeconds();
            var arrivalTime = State.ArrivalTime ?? DateTime.UtcNow.AddSeconds(
                _chainOptions.Value.ChainInfos[orderDto.ToTransfer.ChainId].EstimatedArrivalTime).ToUtcMilliSeconds();
            if (orderDto.Status == OrderStatusEnum.FromTransferConfirmed.ToString() &&
                State.Status == OrderStatusEnum.FromTransferring.ToString())
            {
                arrivalTime = DateTime.UtcNow.AddSeconds(
                    _chainOptions.Value.ChainInfos[orderDto.ToTransfer.ChainId].EstimatedArrivalTime).ToUtcMilliSeconds();
            }
            if (!State.ArrivalTime.HasValue && orderDto.Status == OrderStatusEnum.FromTransferring.ToString())
            {
                var netWorkInfo = _withdrawNetworkOptions.Value.NetworkInfos.FirstOrDefault(t =>
                    t.Coin.Equals(GuidHelper.GenerateId(orderDto.FromTransfer.Network,
                        orderDto.FromTransfer.Symbol), StringComparison.OrdinalIgnoreCase));
                arrivalTime = DateTime.UtcNow.AddSeconds(
                        _chainOptions.Value.ChainInfos[orderDto.ToTransfer.ChainId].EstimatedArrivalTime)
                    .AddSeconds(netWorkInfo.EstimatedArrivalTime).ToUtcMilliSeconds();
            }

            _objectMapper.Map(orderDto, State);
            State.Id = this.GetPrimaryKey();
            State.CreateTime = orderDto.CreateTime = createTime;
            State.LastModifyTime = orderDto.LastModifyTime = now;
            State.ArrivalTime = orderDto.ArrivalTime = arrivalTime;
            if (orderDto.Status == OrderStatusEnum.Finish.ToString())
            {
                State.ArrivalTime = State.LastModifyTime;
            }

            await WriteStateAsync();
            return new CommonResponseDto<DepositOrderDto>(_objectMapper.Map<DepositOrderState, DepositOrderDto>(State));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Deposit order grain create error");
            return new CommonResponseDto<DepositOrderDto>().Error(e);
        }
    }

    public Task<CommonResponseDto<DepositOrderDto>> GetAsync()
    {
        return State.Id == Guid.Empty
            ? Task.FromResult(new CommonResponseDto<DepositOrderDto>(null!))
            : Task.FromResult(
                new CommonResponseDto<DepositOrderDto>(_objectMapper.Map<DepositOrderState, DepositOrderDto>(State)));
    }
}