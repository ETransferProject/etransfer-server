using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.State.Users;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenIssueGrain : IGrainWithGuidKey
{
    Task<bool> AddOrUpdate(UserTokenIssueDto dto);
    Task<UserTokenIssueDto> Get();
}

public class UserTokenIssueGrain : Grain<UserTokenIssueState>, IUserTokenIssueGrain
{
    private readonly IObjectMapper _objectMapper;

    public UserTokenIssueGrain(IObjectMapper objectMapper)
    {
        _objectMapper = objectMapper;
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

    public async Task<bool> AddOrUpdate(UserTokenIssueDto dto)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        State = _objectMapper.Map<UserTokenIssueDto, UserTokenIssueState>(dto);
        State.Id = this.GetPrimaryKey();
        State.CreateTime = State.CreateTime == 0L ? now : State.CreateTime;
        State.UpdateTime = now;
        await WriteStateAsync();
        
        return true;
    }

    public async Task<UserTokenIssueDto> Get()
    {
        if (State.Id == Guid.Empty)
        {
            return null;
        }

        return _objectMapper.Map<UserTokenIssueState, UserTokenIssueDto>(State);
    }
}