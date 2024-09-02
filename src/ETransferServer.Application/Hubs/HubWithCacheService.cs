using System;
using System.Collections.Generic;
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
        await _hubCache.SetAsync(connectionId, new HubDto { ClientId = clientId },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays)
            });
        
        var hubDto = await _hubCache.GetOrAddAsync(clientId, async () => new HubDto
        {
            ExpireTime = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays).ToUnixTimeMilliseconds()
        });
        if (!hubDto.ConnectionIds.Contains(connectionId))
            hubDto.ConnectionIds.Add(connectionId);
        if (hubDto.ConnectionIds.Count > _options.HubLimit)
            hubDto.ConnectionIds.RemoveAt(0);
        await _hubCache.SetAsync(clientId, hubDto, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays)
        });
    }

    public async Task UnRegisterClientAsync(string connectionId)
    {
        var hubDto = await _hubCache.GetAsync(connectionId);
        if (hubDto == null || hubDto.ClientId.IsNullOrEmpty()) return;

        await UnRegisterClientAsync(hubDto.ClientId, connectionId);
    }

    public async Task UnRegisterClientAsync(string clientId, string connectionId)
    {
        await _hubCache.RemoveAsync(connectionId);

        var hubDto = await _hubCache.GetAsync(clientId);
        if (hubDto == null) return;
        hubDto.ConnectionIds.Remove(connectionId);
        if (hubDto.ConnectionIds.Count > 0)
            await _hubCache.SetAsync(clientId, hubDto, new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UnixEpoch.AddMilliseconds(hubDto.ExpireTime)
            });
        else await _hubCache.RemoveAsync(clientId);
    }

    public async Task<List<string>> GetConnectionIdsAsync(string clientId)
    {
        return (await _hubCache.GetAsync(clientId))?.ConnectionIds;
    }
}