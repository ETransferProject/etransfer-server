using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Cache;
using ETransferServer.Dtos.Hub;
using ETransferServer.Options;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs;

public class HubWithCacheService : IHubWithCacheService, ISingletonDependency
{
    private readonly ICacheProvider _cacheProvider;
    private readonly HubOptions _options;

    public HubWithCacheService(ICacheProvider cacheProvider,
        IOptionsSnapshot<HubOptions> options)
    {
        _cacheProvider = cacheProvider;
        _options = options.Value;
    }

    public async Task RegisterClientAsync(List<string> clientIds, string connectionId)
    {
        await _cacheProvider.Set(GetKey(connectionId), Serialize(new HubDto { ClientIds = clientIds }),
            TimeSpan.FromDays(_options.ExpireDays));

        foreach (var clientId in clientIds)
        {
            var hubDto = await _cacheProvider.Get<HubDto>(GetKey(clientId)) ?? new HubDto();
            if (!hubDto.ConnectionIds.Contains(connectionId))
                hubDto.ConnectionIds.Add(connectionId);
            if (hubDto.ConnectionIds.Count > _options.HubLimit)
                hubDto.ConnectionIds.RemoveAt(0);
            hubDto.ExpireTime = DateTimeOffset.UtcNow.AddDays(_options.ExpireDays).ToUnixTimeSeconds();
            await _cacheProvider.Set(GetKey(clientId), Serialize(hubDto), TimeSpan.FromDays(_options.ExpireDays));
        }
    }

    public async Task UnRegisterClientAsync(string connectionId)
    {
        var hubDto = await _cacheProvider.Get<HubDto>(GetKey(connectionId));
        if (hubDto == null || hubDto.ClientIds.IsNullOrEmpty()) return;

        await UnRegisterClientAsync(hubDto.ClientIds, connectionId);
    }

    public async Task UnRegisterClientAsync(List<string> clientIds, string connectionId)
    {
        await _cacheProvider.Delete(GetKey(connectionId));

        foreach (var clientId in clientIds)
        {
            var hubDto = await _cacheProvider.Get<HubDto>(GetKey(clientId));
            if (hubDto == null) continue;
            hubDto.ConnectionIds.Remove(connectionId);
            if (hubDto.ConnectionIds.Count > 0)
                await _cacheProvider.Set(GetKey(clientId), Serialize(hubDto),
                    TimeSpan.FromSeconds(hubDto.ExpireTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            else await _cacheProvider.Delete(GetKey(clientId));
        }
    }

    public async Task<List<string>> GetConnectionIdsAsync(List<string> clientIds)
    {
        var connectionIds = new List<string>();
        foreach (var clientId in clientIds)
        {
            var ids = (await _cacheProvider.Get<HubDto>(GetKey(clientId)))?.ConnectionIds;
            if (ids.IsNullOrEmpty()) continue;
            connectionIds = connectionIds.Union(ids).ToList();
        }

        return connectionIds;
    }
    
    public async Task<List<string>> GetClientIdsAsync(string connectionId)
    {
        return (await _cacheProvider.Get<HubDto>(GetKey(connectionId)))?.ClientIds;
    }
    
    private string Serialize(object val)
    {
        var serializeSetting = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        return JsonConvert.SerializeObject(val, Formatting.None, serializeSetting);
    }
    
    private string GetKey(string key)
    {
        return $"ETransfer:{key}";
    }
}