using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Order;
using ETransferServer.User;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs
{
    public class OrderChangeHandler : IConsumer<OrderChangeEto>, ITransientDependency
    {
        private readonly IHubContext<EtransferHub> _hubContext;
        private readonly IHubConnectionProvider _hubConnectionProvider;
        private readonly IUserAppService _userAppService;
        private readonly IOrderAppService _orderAppService;
        private readonly IClusterClient _clusterClient;
        private readonly ILogger<OrderChangeHandler> _logger;
        
        public OrderChangeHandler(IHubConnectionProvider hubConnectionProvider,
            IHubContext<EtransferHub> hubContext,
            IUserAppService userAppService,
            IOrderAppService orderAppService,
            IClusterClient clusterClient,
            ILogger<OrderChangeHandler> logger)
        {
            _hubConnectionProvider = hubConnectionProvider;
            _hubContext = hubContext;
            _userAppService = userAppService;
            _orderAppService = orderAppService;
            _clusterClient = clusterClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderChangeEto> eventData)
        {
            var (addressList, userAddressList) = await GetAddressListAsync(eventData.Message);
            _logger.LogInformation("OrderChangeHandler, userId: {userId}, address: {addressList}, order: {order}",
                eventData.Message.UserId, String.Join(CommonConstant.Comma, addressList), JsonConvert.SerializeObject(eventData.Message));
            if (addressList.IsNullOrEmpty()) return;
            var connectionIds = await _hubConnectionProvider.GetUserConnections(addressList);
            if (connectionIds.IsNullOrEmpty()) return;
            _logger.LogInformation("OrderChangeHandler, connectionIds: {connectionIds}",
                string.Join(CommonConstant.Comma, connectionIds));
            var result = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto
            {
                Address = addressList.Count == 3 ? addressList[0] : null,
                AddressList = userAddressList,
                Time = 0
            }, eventData.Message);
            _logger.LogInformation(
                "OrderChangeHandler, address: {addressList}, time: {time}, pending: {depositCount1},{transferCount1}, success: {depositCount2},{transferCount2}, fail: {depositCount3},{transferCount3}",
                String.Join(CommonConstant.Comma, addressList), 0, result?.Processing.DepositCount, 
                result?.Processing.TransferCount, result?.Succeed.DepositCount, result?.Succeed.TransferCount, 
                result?.Failed.DepositCount, result?.Failed.TransferCount);
            foreach (var connectionId in connectionIds)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUserOrderRecords", result);
            }
        }

        private async Task<Tuple<List<string>, List<GetUserAddressDto>>> GetAddressListAsync(OrderChangeEto eto)
        {
            var userDto = await _userAppService.GetUserByIdAsync(eto.UserId.ToString());
            var address = userDto?.AddressInfos?.FirstOrDefault()?.Address;
            var addressList = address.IsNullOrEmpty() ? new List<string>() : new List<string> { address };
            addressList.Add(await GetFullAddressAsync(eto.FromTransfer.Network, eto.FromTransfer.FromAddress));
            addressList.Add(await GetFullAddressAsync(eto.ToTransfer.Network, eto.ToTransfer.ToAddress));

            var userAddressList = address.IsNullOrEmpty()
                ? new List<GetUserAddressDto>()
                : new List<GetUserAddressDto> { new() { SourceType = userDto?.AppId, Address = address }};
            userAddressList.Add(new() { SourceType = await GetWalletTypeAsync(eto.FromTransfer.Network) ?? WalletEnum.Portkey.ToString(), Address = eto.FromTransfer.FromAddress });
            userAddressList.Add(new() { SourceType = await GetWalletTypeAsync(eto.ToTransfer.Network) ?? WalletEnum.Portkey.ToString(), Address = eto.ToTransfer.ToAddress });
            return Tuple.Create(addressList, userAddressList);
        }
        
        private async Task<string> GetFullAddressAsync(string network, string address)
        {
            var walletType = await GetWalletTypeAsync(network);
            if (walletType.IsNullOrEmpty()) return address;
            return string.Concat(walletType, CommonConstant.Underline, address);
        }

        private async Task<string> GetWalletTypeAsync(string network)
        {
            if (network == ChainId.AELF) return null;
            if (Enum.TryParse<WalletEnum>(network, true, out var walletType) 
                && (int)walletType > 2) return network.ToLower();
            return WalletEnum.EVM.ToString().ToLower();
        }
    }
}