using Orleans;
using ETransferServer.Grains.State.Users;
using ETransferServer.User;
using ETransferServer.User.Dtos;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserGrain : IGrainWithGuidKey
{
    Task<GrainResultDto<UserGrainDto>> CreateUser(UserGrainDto input);
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

    public async Task<GrainResultDto<UserGrainDto>> CreateUser(UserGrainDto input)
    {
        State.Id = this.GetPrimaryKey();
        State.UserId = this.GetPrimaryKey();
        State.AddressInfos = input.AddressInfos;
        State.AppId = input.AppId;
        State.CaHash = input.CaHash;
        State.CreateTime = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        State.ModificationTime = State.CreateTime;

        await WriteStateAsync();

        await _userAppService.CreateUserAsync(_objectMapper.Map<UserState, UserDto>(State));
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
                Message = "user not exists."
            });
        }
        
        return Task.FromResult(new GrainResultDto<UserGrainDto>()
        {
            Success = true,
            Data = _objectMapper.Map<UserState, UserGrainDto>(State)
        });
    }
}