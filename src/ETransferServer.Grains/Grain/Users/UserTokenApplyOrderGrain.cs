using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.State.Users;
using ETransferServer.TokenAccess;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenApplyOrderGrain : IGrainWithGuidKey
{
    Task<bool> AddOrUpdate(TokenApplyOrderDto dto);
    Task<TokenApplyOrderDto> Get();
}

public class UserTokenApplyOrderGrain : Grain<UserTokenApplyOrderState>, IUserTokenApplyOrderGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ITokenAccessAppService _tokenAccessAppService;

    public UserTokenApplyOrderGrain(IObjectMapper objectMapper,
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

    public async Task<bool> AddOrUpdate(TokenApplyOrderDto dto)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        State = _objectMapper.Map<TokenApplyOrderDto, UserTokenApplyOrderState>(dto);
        State.Id = this.GetPrimaryKey();
        State.CreateTime = State.CreateTime == 0L ? now : State.CreateTime;
        State.UpdateTime = now;
        await WriteStateAsync();
        
        await _tokenAccessAppService.AddOrUpdateUserTokenApplyOrderAsync(
            _objectMapper.Map<UserTokenApplyOrderState, TokenApplyOrderDto>(State));
        return true;
    }

    public async Task<TokenApplyOrderDto> Get()
    {
        if (State.Id == Guid.Empty)
        {
            return null;
        }

        return _objectMapper.Map<UserTokenApplyOrderState, TokenApplyOrderDto>(State);
    }
}