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
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Hubs
{
    public class OrderChangeHandler : IConsumer<OrderChangeEto>, ITransientDependency
    {
        private readonly IHubContext<EtransferHub> _hubContext;
        private readonly IHubConnectionProvider _hubConnectionProvider;
        private readonly IUserAppService _userAppService;
        private readonly IOrderAppService _orderAppService;
        private readonly ILogger<OrderChangeHandler> _logger;
        
        public OrderChangeHandler(IHubConnectionProvider hubConnectionProvider,
            IHubContext<EtransferHub> hubContext,
            IUserAppService userAppService,
            IOrderAppService orderAppService,
            ILogger<OrderChangeHandler> logger)
        {
            _hubConnectionProvider = hubConnectionProvider;
            _hubContext = hubContext;
            _userAppService = userAppService;
            _orderAppService = orderAppService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderChangeEto> eventData)
        {
            var addressList = await GetAddressListAsync(eventData.Message);
            _logger.LogInformation("OrderChangeHandler, userId: {userId}, address: {addressList}, order: {order}",
                eventData.Message.UserId, String.Join(CommonConstant.Comma, addressList), JsonConvert.SerializeObject(eventData.Message));
            if (addressList.IsNullOrEmpty()) return;
            var connectionIds = await _hubConnectionProvider.GetUserConnections(addressList);
            if (connectionIds.IsNullOrEmpty()) return;
            _logger.LogInformation("OrderChangeHandler, connectionIds: {connectionIds}",
                string.Join(CommonConstant.Comma, connectionIds));
            foreach (var connectionId in connectionIds)
            {
                var addresses = await _hubConnectionProvider.GetUserAddresses(connectionId);
                var userAddressList = await GetUserAddressListAsync(addresses);
                var result = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto
                {
                    AddressList = userAddressList,
                    Time = 0
                }, eventData.Message);
                _logger.LogInformation(
                    "OrderChangeHandler, connectionId: {connectionId}, address: {addressList}, time: {time}, pending: {depositCount1},{transferCount1}, success: {depositCount2},{transferCount2}, fail: {depositCount3},{transferCount3}",
                    connectionId, JsonConvert.SerializeObject(userAddressList), 0, result?.Processing.DepositCount, 
                    result?.Processing.TransferCount, result?.Succeed.DepositCount, result?.Succeed.TransferCount, 
                    result?.Failed.DepositCount, result?.Failed.TransferCount);
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUserOrderRecords", result);
            }
        }

        private async Task<List<string>> GetAddressListAsync(OrderChangeEto eto)
        {
            var userDto = await _userAppService.GetUserByIdAsync(eto.UserId.ToString());
            var address = userDto?.AddressInfos?.FirstOrDefault()?.Address;
            var addressList = address.IsNullOrEmpty() ? new List<string>() : new List<string> { address };
            return addressList;
        }
        
        private async Task<List<GetUserAddressDto>> GetUserAddressListAsync(List<string> addressList)
        {
            var userAddressList = new List<GetUserAddressDto>();
            foreach (var address in addressList)
            {
                var dto = new GetUserAddressDto();
                if (address.StartsWith(string.Concat(WalletEnum.EVM.ToString().ToLower(), CommonConstant.Underline)))
                {
                    dto.SourceType = WalletEnum.EVM.ToString();
                    dto.Address = address.Substring(WalletEnum.EVM.ToString().Length + 1);
                }
                else if (address.StartsWith(string.Concat(WalletEnum.Solana.ToString().ToLower(), CommonConstant.Underline)))
                {
                    dto.SourceType = WalletEnum.Solana.ToString();
                    dto.Address = address.Substring(WalletEnum.Solana.ToString().Length + 1);
                }
                else if (address.StartsWith(string.Concat(WalletEnum.TRX.ToString().ToLower(), CommonConstant.Underline)))
                {
                    dto.SourceType = WalletEnum.TRX.ToString();
                    dto.Address = address.Substring(WalletEnum.TRX.ToString().Length + 1);
                }
                else if (address.StartsWith(string.Concat(WalletEnum.TON.ToString().ToLower(), CommonConstant.Underline)))
                {
                    dto.SourceType = WalletEnum.TON.ToString();
                    dto.Address = address.Substring(WalletEnum.TON.ToString().Length + 1);
                }
                else
                {
                    dto.SourceType = WalletEnum.Portkey.ToString();
                    dto.Address = address;
                }
                userAddressList.Add(dto);
            }

            return userAddressList;
        }
    }
}