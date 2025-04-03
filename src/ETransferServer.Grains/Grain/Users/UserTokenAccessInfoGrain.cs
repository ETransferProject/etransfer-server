using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.State.Users;
using ETransferServer.TokenAccess;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenAccessInfoGrain : IGrainWithStringKey
{
    Task<bool> AddOrUpdate(UserTokenAccessInfoDto dto);
    Task<UserTokenAccessInfoDto> Get();
}

public class UserTokenAccessInfoGrain : Grain<UserTokenAccessInfoState>, IUserTokenAccessInfoGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ITokenAccessAppService _tokenAccessAppService;

    public UserTokenAccessInfoGrain(IObjectMapper objectMapper,
        ITokenAccessAppService tokenAccessAppService)
    {
        _objectMapper = objectMapper;
        _tokenAccessAppService = tokenAccessAppService;
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

    public async Task<bool> AddOrUpdate(UserTokenAccessInfoDto dto)
    {
        State = _objectMapper.Map<UserTokenAccessInfoDto, UserTokenAccessInfoState>(dto);
        State.Id = GuidHelper.UniqGuid(dto.Symbol, dto.UserAddress);
        await WriteStateAsync();

        await _tokenAccessAppService.AddOrUpdateUserTokenAccessInfoAsync(
            _objectMapper.Map<UserTokenAccessInfoState, UserTokenAccessInfoDto>(State));
        return true;
    }

    public async Task<UserTokenAccessInfoDto> Get()
    {
        if (State.Id == Guid.Empty)
        {
            return null;
        }

        return _objectMapper.Map<UserTokenAccessInfoState, UserTokenAccessInfoDto>(State);
    }
}