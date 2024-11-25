using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            _logger.LogInformation(
                "RequestUserOrderRecord address: {address}, addressList: {addressList}, time: {time}, " +
                "connectionId: {connectionId}", input.Address,
                !input.AddressList.IsNullOrEmpty() ? JsonConvert.SerializeObject(input.AddressList) : null, input.Time,
                Context.ConnectionId);
            if (input.Address.IsNullOrEmpty() && input.AddressList.IsNullOrEmpty()) return;
            var addressList = await GetAddressListAsync(input);
            await _hubConnectionProvider.AddUserConnection(addressList, Context.ConnectionId);
            var records = await _orderAppService.GetUserOrderRecordListAsync(input);
            await Clients.Caller.SendAsync("ReceiveUserOrderRecords", records);
        }

        public async Task UnsubscribeUserOrderRecord(GetUserOrderRecordRequestDto input)
        {
            _logger.LogInformation("UnsubscribeUserOrderRecord address: {address}, connectionId: {connectionId}, " +
                                   "addressList: {addressList}", input.Address, Context.ConnectionId,
                !input.AddressList.IsNullOrEmpty() ? JsonConvert.SerializeObject(input.AddressList) : null);
            if (input.Address.IsNullOrEmpty() && input.AddressList.IsNullOrEmpty()) return;
            var addressList = await GetAddressListAsync(input);
            await _hubConnectionProvider.ClearUserConnection(addressList, Context.ConnectionId);
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("OnDisconnectedAsync connectionId: {connectionId}", Context.ConnectionId);
            await _hubConnectionProvider.ClearUserConnection(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        private async Task<List<string>> GetAddressListAsync(GetUserOrderRecordRequestDto input)
        {
            var addressList = input.Address.IsNullOrEmpty() ? new List<string>() : new List<string> { input.Address };
            if (!input.AddressList.IsNullOrEmpty())
                addressList = addressList.Union(input.AddressList.ConvertAll(t =>
                    Enum.TryParse<WalletEnum>(t.SourceType, true, out var walletType)
                    && (int)walletType > 1
                        ? string.Concat(t.SourceType.ToLower(), CommonConstant.Underline, t.Address)
                        : t.Address)).ToList();
            return addressList;
        }
    }
}