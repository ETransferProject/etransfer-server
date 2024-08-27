using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.SignalR;

namespace ETransferServer.Hubs
{
    public class ETransferHub : AbpHub
    {
        private readonly IOrderAppService _orderAppService;
        private readonly IHubConnectionProvider _hubConnectionProvider;
        private readonly ILogger<ETransferHub> _logger;

        public ETransferHub(IOrderAppService orderAppService,
            IHubConnectionProvider hubConnectionProvider,
            ILogger<ETransferHub> logger)
        {
            _orderAppService = orderAppService;
            _hubConnectionProvider = hubConnectionProvider;
            _logger = logger;
        }

        public async Task RequestUserOrderRecord(string address, long? minTimestamp)
        {
            _hubConnectionProvider.AddUserConnection(address, Context.ConnectionId);
            var records = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto
            {
                Address = address,
                MinTimestamp = minTimestamp
            });
            _logger.LogInformation("RequestUserOrderRecord address: {address}, minTimestamp: {MinTimestamp}",
                address, minTimestamp);
            await Clients.Caller.SendAsync("ReceiveUserOrderRecords", records);
        }

        public async Task UnsubscribeUserOrderRecord(string address, long? timestamp)
        {
            _hubConnectionProvider.ClearUserConnection(address, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _hubConnectionProvider.ClearUserConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}