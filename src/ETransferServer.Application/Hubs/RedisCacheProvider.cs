using System;
using System.Threading.Tasks;
using ETransferServer.Cache;
using Newtonsoft.Json;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs;

public class RedisCacheProvider : ICacheProvider, ISingletonDependency
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly IDatabase _database;

    public RedisCacheProvider(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = _connectionMultiplexer.GetDatabase();
    }

    public async Task Set(string key, string value, TimeSpan? expire)
    {
        await _database.StringSetAsync(key, value, expiry: expire);
    }
    public async Task Set<T>(string key, T? value, TimeSpan? expire) where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), "redis cache set error, value can not be null.");
        }

        await _database.StringSetAsync(key, JsonConvert.SerializeObject(value), expiry: expire);
    }

    public async Task<T?> Get<T>(string key) where T : class
    {
        var value = await _database.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;

        return JsonConvert.DeserializeObject<T>(value);
    }

    public async Task Delete(string key)
    {
        _database.KeyDelete(key);
    }
}