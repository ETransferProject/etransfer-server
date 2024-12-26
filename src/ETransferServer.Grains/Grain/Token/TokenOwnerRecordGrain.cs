using ETransferServer.Common;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Grains.State.Token;
using Microsoft.Extensions.Logging;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenOwnerRecordGrain : IGrainWithStringKey
{
    Task AddOrUpdate(TokenOwnerListDto dto);
    
    Task<TokenOwnerListDto> Get();
}

public class TokenOwnerRecordGrain : Grain<TokenOwnerRecordState>, ITokenOwnerRecordGrain
{
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<TokenOwnerRecordGrain> _logger;
    private const int TokenOwnerRecordThreshold = 1000;

    public TokenOwnerRecordGrain(IObjectMapper objectMapper,
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
        if (!State.CreateTime.HasValue)
        {
            State = _objectMapper.Map<TokenOwnerListDto, TokenOwnerRecordState>(dto) ?? new TokenOwnerRecordState();
            State.CreateTime = now;
        }
        else
        {
            State.TokenOwnerList.AddRange(dto.TokenOwnerList);
        }

        if (State.TokenOwnerList.Count >= TokenOwnerRecordThreshold)
        {
            _logger.LogWarning("TokenOwnerRecordGrain exceed, {count},{threshold}", 
                State.TokenOwnerList.Count, TokenOwnerRecordThreshold);
        }

        State.UpdateTime = now;
        await WriteStateAsync();
    }
    
    public async Task<TokenOwnerListDto> Get()
    {
        return _objectMapper.Map<TokenOwnerRecordState, TokenOwnerListDto>(State);
    }
}