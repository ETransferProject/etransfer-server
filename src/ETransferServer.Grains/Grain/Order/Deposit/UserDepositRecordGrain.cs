using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.State.Order;
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
    private readonly IOptionsMonitor<TimerOptions> _timerOptions;

    public UserDepositRecordGrain(IObjectMapper objectMapper, ILogger<UserDepositRecordGrain> logger1, IOptionsMonitor<TimerOptions> timerOptions)
    {
        _objectMapper = objectMapper;
        _logger = logger1;
        _timerOptions = timerOptions;
    }

    public async Task<CommonResponseDto<DepositOrderDto>> CreateOrUpdateAsync(DepositOrderDto orderDto)
    {
        try
        {
            var now = DateTime.UtcNow.ToUtcMilliSeconds();
            var createTime = State.CreateTime ?? DateTime.UtcNow.ToUtcMilliSeconds();
            
            _objectMapper.Map(orderDto, State);
            State.Id = this.GetPrimaryKey();
            State.CreateTime = createTime;
            State.LastModifyTime = now;

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