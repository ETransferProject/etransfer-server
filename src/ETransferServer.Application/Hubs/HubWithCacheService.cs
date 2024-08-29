using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Hub;
using ETransferServer.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs;

public class HubWithCacheService : IHubWithCacheService, ISingletonDependency
{
    private readonly IDistributedCache<HubDto> _hubCache;
    private readonly HubOptions _options;

    public HubWithCacheService(IDistributedCache<HubDto> hubCache,
        IOptionsSnapshot<HubOptions> options)
    {
        _hubCache = hubCache;
        _options = options.Value;
    }

    public async Task RegisterClientAsync(string clientId, string connectionId)
    {
        await _hubCache.SetAsync(clientId, new HubDto { ConnectionId = connectionId }, 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays)
            });
        await _hubCache.SetAsync(connectionId, new HubDto { ClientId = clientId }, 
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays)
            });
    }

    public async Task UnRegisterClientAsync(string connectionId)
    {
        var hubDto = await _hubCache.GetAsync(connectionId);
        if (hubDto == null || hubDto.ClientId.IsNullOrEmpty()) return;

        await _hubCache.RemoveAsync(connectionId);
        await _hubCache.RemoveAsync(hubDto.ClientId);
    }
    
    public async Task UnRegisterClientAsync(string clientId, string connectionId)
    {
        await _hubCache.RemoveAsync(clientId);
        await _hubCache.RemoveAsync(connectionId);
    }

    public async Task<string> GetConnectionIdAsync(string clientId)
    {
        return (await _hubCache.GetAsync(clientId))?.ConnectionId;
    }
}