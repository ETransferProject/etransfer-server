using ETransferServer.Grains.State.Users;
using Orleans;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserReconciliationGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<UserReconciliationDto>> AddOrUpdateUser(UserReconciliationDto input);
    Task<GrainResultDto<UserReconciliationDto>> GetUser();
}

public class UserReconciliationGrain : Grain<UserReconciliationState>, IUserReconciliationGrain
{
    private readonly IObjectMapper _objectMapper;

    public UserReconciliationGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
    }

    public override async Task OnActivateAsync()
    {
        await ReadStateAsync();
        await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync()
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync();
    }

    public async Task<GrainResultDto<UserReconciliationDto>> AddOrUpdateUser(UserReconciliationDto input)
    {
        State.Id = this.GetPrimaryKey();
        State.UserName = input.UserName;
        State.Address = input.Address;
        State.PasswordHash = input.PasswordHash;
        await WriteStateAsync();

        return new GrainResultDto<UserReconciliationDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserReconciliationState, UserReconciliationDto>(State)
        };
    }

    public Task<GrainResultDto<UserReconciliationDto>> GetUser()
    {
        if (State.Id == Guid.Empty)
        {
            return Task.FromResult(new GrainResultDto<UserReconciliationDto>
            {
                Success = false,
                Message = "User not exists."
            });
        }
        
        return Task.FromResult(new GrainResultDto<UserReconciliationDto>
        {
            Success = true,
            Data = _objectMapper.Map<UserReconciliationState, UserReconciliationDto>(State)
        });
    }
}