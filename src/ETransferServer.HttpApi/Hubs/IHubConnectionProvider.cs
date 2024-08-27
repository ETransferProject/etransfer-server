using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs
{
    public interface IHubConnectionProvider
    {
        void AddUserConnection(string address, string connectionId);
        string GetUserConnection(string address);
        void ClearUserConnection(string address, string connectionId);
        void ClearUserConnection(string connectionId);
    }
    
    public class HubConnectionProvider : IHubConnectionProvider, ISingletonDependency
    {
        private readonly ConcurrentDictionary<string, string> _userConnections = new();
        private readonly ConcurrentDictionary<string, string> _userConnectionIds = new();

        public void AddUserConnection(string address, string connectionId)
        {
            if (_userConnectionIds.TryAdd(connectionId, address))
            {
                _userConnections.TryAdd(address, connectionId);
            }
        }

        public string GetUserConnection(string address)
        {
            _userConnections.TryGetValue(address, out var value);
            return value;
        }

        public void ClearUserConnection(string address, string connectionId)
        {
            _userConnections.TryRemove(address, out _);
            _userConnectionIds.TryRemove(connectionId, out _);
        }
        
        public void ClearUserConnection(string connectionId)
        {
            if (_userConnectionIds.TryRemove(connectionId, out var key))
            {
                _userConnections.TryRemove(key, out _);
            }
        }
    }
}