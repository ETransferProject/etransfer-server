using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs
{
    public interface IHubConnectionProvider
    {
        Task AddUserConnection(string address, string connectionId);
        Task<string> GetUserConnection(string address);
        Task ClearUserConnection(string address, string connectionId);
        Task ClearUserConnection(string connectionId);
    }
    
    public class HubConnectionProvider : IHubConnectionProvider, ISingletonDependency
    {
        private readonly IHubWithCacheService _hubAppService;

        public HubConnectionProvider(IHubWithCacheService hubAppService)
        {
            _hubAppService = hubAppService;
        }

        public async Task AddUserConnection(string address, string connectionId)
        {
            await _hubAppService.RegisterClientAsync(address, connectionId);
        }

        public async Task<string> GetUserConnection(string address)
        {
            return await _hubAppService.GetConnectionIdAsync(address);
        }

        public async Task ClearUserConnection(string address, string connectionId)
        {
            await _hubAppService.UnRegisterClientAsync(address, connectionId);
        }
        
        public async Task ClearUserConnection(string connectionId)
        {
            await _hubAppService.UnRegisterClientAsync(connectionId);
        }
    }
}