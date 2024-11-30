using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs
{
    public interface IHubConnectionProvider
    {
        Task AddUserConnection(List<string> addresses, string connectionId);
        Task<List<string>> GetUserConnections(List<string> addresses);
        Task<List<string>> GetUserAddresses(string connectionId);
        Task ClearUserConnection(List<string> addresses, string connectionId);
        Task ClearUserConnection(string connectionId);
    }
    
    public class HubConnectionProvider : IHubConnectionProvider, ISingletonDependency
    {
        private readonly IHubWithCacheService _hubAppService;

        public HubConnectionProvider(IHubWithCacheService hubAppService)
        {
            _hubAppService = hubAppService;
        }

        public async Task AddUserConnection(List<string> addresses, string connectionId)
        {
            await _hubAppService.RegisterClientAsync(addresses, connectionId);
        }

        public async Task<List<string>> GetUserConnections(List<string> addresses)
        {
            return await _hubAppService.GetConnectionIdsAsync(addresses);
        }

        public async Task<List<string>> GetUserAddresses(string connectionId)
        {
            return await _hubAppService.GetClientIdsAsync(connectionId);
        }

        public async Task ClearUserConnection(List<string> addresses, string connectionId)
        {
            await _hubAppService.UnRegisterClientAsync(addresses, connectionId);
        }
        
        public async Task ClearUserConnection(string connectionId)
        {
            await _hubAppService.UnRegisterClientAsync(connectionId);
        }
    }
}