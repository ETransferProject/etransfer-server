using Microsoft.Extensions.Logging;
using Orleans;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.State.Order;
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

    public UserWithdrawRecordGrain(IObjectMapper objectMapper, ILogger<UserWithdrawRecordGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
    }

    public async Task<CommonResponseDto<WithdrawOrderDto>> AddOrUpdateAsync(WithdrawOrderDto orderDto)
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