using ETransferServer.Grains.State.Users;
using ETransferServer.User;
using ETransferServer.User.Dtos;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<UserGrainDto>> AddOrUpdateUser(UserGrainDto input);
    Task<GrainResultDto<UserGrainDto>> GetUser();
}

public class UserGrain : Grain<UserState>, IUserGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly IUserAppService _userAppService;

    public UserGrain(IObjectMapper objectMapper, IUserAppService userAppService)
    {
        _objectMapper = objectMapper;
        _userAppService = userAppService;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ReadStateAsync();
        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        await WriteStateAsync();
        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task<GrainResultDto<UserGrainDto>> AddOrUpdateUser(UserGrainDto input)
    {
        State.Id = this.GetPrimaryKey();
        State.UserId = this.GetPrimaryKey();
        State.AddressInfos = input.AddressInfos;
        State.AppId = input.AppId;
        State.CaHash = input.CaHash;
        State.ModificationTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        State.CreateTime = State.CreateTime > 0
            ? State.CreateTime
            : State.ModificationTime;
        
        await WriteStateAsync();

        await _userAppService.AddOrUpdateUserAsync(_objectMapper.Map<UserState, UserDto>(State));
        return new GrainResultDto<UserGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserState, UserGrainDto>(State)
        };
    }

    public Task<GrainResultDto<UserGrainDto>> GetUser()
    {
        if (State.Id == Guid.Empty)
        {
            return Task.FromResult(new GrainResultDto<UserGrainDto>()
            {
                Success = false,
                Message = "User not exists."
            });
        }
        
        return Task.FromResult(new GrainResultDto<UserGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserState, UserGrainDto>(State)
        });
    }
}