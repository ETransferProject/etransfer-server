using ETransferServer.Common;
using ETransferServer.Dtos.Token;
using ETransferServer.Grains.State.Token;
using Microsoft.Extensions.Logging;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grains.Grain.Token;

public interface ITokenPoolGrain : IGrainWithStringKey
{
    Task AddOrUpdate(TokenPoolDto dto);
    Task<TokenPoolDto> Get();

    public static string GenerateGrainId()
    {
        return DateTime.UtcNow.Date.ToUtcString(TimeHelper.DatePattern);
    }
    
    public static string GenerateGrainId(long timestamp)
    {
        return TimeHelper.GetDateTimeFromTimeStamp(timestamp).Date.ToUtcString(TimeHelper.DatePattern);
    }
}

public class TokenPoolGrain : Grain<TokenPoolState>, ITokenPoolGrain
{
    private readonly ILogger<TokenPoolGrain> _logger;
    private readonly IObjectMapper _objectMapper;

    public TokenPoolGrain(IObjectMapper objectMapper, ILogger<TokenPoolGrain> logger)
    {
        _objectMapper = objectMapper;
        _logger = logger;
    }
    
    public async Task AddOrUpdate(TokenPoolDto dto)
    {
        _objectMapper.Map(dto, State);
        State.Date = this.GetPrimaryKeyString();
        State.LastModifyTime = DateTime.UtcNow.ToUtcMilliSeconds();
        
        await WriteStateAsync();
        _logger.LogInformation("Save token pool");
    }

    public async Task<TokenPoolDto> Get()
    {
        if (State.LastModifyTime > 0)
        {
            return _objectMapper.Map<TokenPoolState, TokenPoolDto>(State);
        }
        return null;
    }
}