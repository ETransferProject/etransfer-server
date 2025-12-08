using System;
using System.Threading.Tasks;

namespace ETransferServer.Cache;

public interface ICacheProvider
{
    Task Set(string key, string value, TimeSpan? expire);
    Task Set<T>(string key, T value, TimeSpan? expire) where T : class;
    Task<T> Get<T>(string key) where T : class;
    Task Delete(string key);
}