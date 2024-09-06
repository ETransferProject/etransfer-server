using System.Collections.Generic;
using System.Threading.Tasks;

namespace ETransferServer.Hubs;

public interface IHubWithCacheService
{
    Task RegisterClientAsync(string clientId, string connectionId);
    Task UnRegisterClientAsync(string connectionId);
    Task UnRegisterClientAsync(string clientId, string connectionId);
    Task<List<string>> GetConnectionIdsAsync(string clientId);
}