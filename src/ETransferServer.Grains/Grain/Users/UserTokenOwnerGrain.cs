using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.Grain.Token;
using ETransferServer.Grains.State.Users;
using Microsoft.Extensions.Logging;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserTokenOwnerGrain : IGrainWithStringKey
{
    Task AddOrUpdate(TokenOwnerListDto dto);
    
    Task<TokenOwnerListDto> Get();
}

public class UserTokenOwnerGrain : Grain<UserTokenOwnerState>, IUserTokenOwnerGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenOwnerRecordGrain> _logger;
    private const int UserTokenOwnerThreshold = 1000;

    public UserTokenOwnerGrain(IObjectMapper objectMapper,
        ILogger<TokenOwnerRecordGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
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

    public async Task AddOrUpdate(TokenOwnerListDto dto)
    {
        var now = DateTime.UtcNow.ToUtcMilliSeconds();
        State = _objectMapper.Map<TokenOwnerListDto, UserTokenOwnerState>(dto) ?? new UserTokenOwnerState();
        State.Address = this.GetPrimaryKeyString();
        if (State.TokenOwnerList.Count >= UserTokenOwnerThreshold)
        {
            _logger.LogWarning("UserTokenOwnerGrain exceed, {count},{threshold}", 
                State.TokenOwnerList.Count, UserTokenOwnerThreshold);
        }
        State.CreateTime ??= now;
        State.UpdateTime = now;
        await WriteStateAsync();
    }
    
    public async Task<TokenOwnerListDto> Get()
    {
        return _objectMapper.Map<UserTokenOwnerState, TokenOwnerListDto>(State);
    }
}