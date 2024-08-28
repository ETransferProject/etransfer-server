using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.SignalR;

namespace ETransferServer.Hubs
{
    public class EtransferHub : AbpHub
    {
        private readonly IOrderAppService _orderAppService;
        private readonly IEtransferHubConnectionProvider _hubConnectionProvider;
        private readonly ILogger<EtransferHub> _logger;

        public EtransferHub(IOrderAppService orderAppService,
            IEtransferHubConnectionProvider hubConnectionProvider,
            ILogger<EtransferHub> logger)
        {
            _orderAppService = orderAppService;
            _hubConnectionProvider = hubConnectionProvider;
            _logger = logger;
        }

        public async Task RequestUserOrderRecord(GetUserOrderRecordRequestDto input)
        {
            _logger.LogInformation("RequestUserOrderRecord address: {address}, connectionId: {connectionId}",
                input.Address, Context.ConnectionId);
            _hubConnectionProvider.AddUserConnection(input.Address, Context.ConnectionId);
            var records = await _orderAppService.GetUserOrderRecordListAsync(input);
            _logger.LogInformation("RequestUserOrderRecord address: {address}, minTimestamp: {MinTimestamp}",
                input.Address, input.MinTimestamp);
            await Clients.Caller.SendAsync("ReceiveUserOrderRecords", records);
        }

        public async Task UnsubscribeUserOrderRecord(GetUserOrderRecordRequestDto input)
        {
            _hubConnectionProvider.ClearUserConnection(input.Address, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _hubConnectionProvider.ClearUserConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}