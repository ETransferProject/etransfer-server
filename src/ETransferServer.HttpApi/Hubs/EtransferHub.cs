using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Order;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp.AspNetCore.SignalR;

namespace ETransferServer.Hubs
{
    public class EtransferHub : AbpHub
    {
        private readonly IOrderAppService _orderAppService;
        private readonly IHubConnectionProvider _hubConnectionProvider;
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<EtransferHub> _logger;

        public EtransferHub(IOrderAppService orderAppService,
            IHubConnectionProvider hubConnectionProvider,
            IClusterClient clusterClient,
            ILogger<EtransferHub> logger)
        {
            _orderAppService = orderAppService;
            _hubConnectionProvider = hubConnectionProvider;
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public async Task RequestUserOrderRecord(GetUserOrderRecordRequestDto input)
        {
            _logger.LogInformation("RequestUserOrderRecord address: {address}, time: {time}, " +
                                   "connectionId: {connectionId}", input.Address, input.Time, Context.ConnectionId);
            await _hubConnectionProvider.AddUserConnection(input.Address, Context.ConnectionId);
            var orderChangeGrain = _clusterClient.GetGrain<IUserOrderChangeGrain>(input.Address);
            await orderChangeGrain.AddOrUpdate(input.Time);
            var records = await _orderAppService.GetUserOrderRecordListAsync(input);
            await Clients.Caller.SendAsync("ReceiveUserOrderRecords", records);
        }

        public async Task UnsubscribeUserOrderRecord(GetUserOrderRecordRequestDto input)
        {
            await _hubConnectionProvider.ClearUserConnection(input.Address, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _hubConnectionProvider.ClearUserConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}