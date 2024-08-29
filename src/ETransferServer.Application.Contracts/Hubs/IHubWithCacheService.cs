using System.Threading.Tasks;

namespace ETransferServer.Hubs;

public interface IHubWithCacheService
{
    Task RegisterClientAsync(string clientId, string connectionId);
    Task UnRegisterClientAsync(string connectionId);
    Task UnRegisterClientAsync(string clientId, string connectionId);
    Task<string> GetConnectionIdAsync(string clientId);
}