using System.Collections.Generic;
using System.Threading.Tasks;

namespace ETransferServer.Hubs;

public interface IHubWithCacheService
{
    Task RegisterClientAsync(List<string> clientIds, string connectionId);
    Task UnRegisterClientAsync(string connectionId);
    Task UnRegisterClientAsync(List<string> clientIds, string connectionId);
    Task<List<string>> GetConnectionIdsAsync(List<string> clientIds);
    Task<List<string>> GetClientIdsAsync(string connectionId);
}