using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Order;
using ETransferServer.User;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
            var userDto = await _userAppService.GetUserByIdAsync(eventData.Message.UserId.ToString());
            var address = userDto?.AddressInfos?.FirstOrDefault()?.Address;
            _logger.LogInformation("OrderChangeHandler, userId: {userId}, address: {address}",
                eventData.Message.UserId, address);
            if (address.IsNullOrEmpty()) return;
            var connectionIds = await _hubConnectionProvider.GetUserConnections(address);
            if (connectionIds.IsNullOrEmpty()) return;
            _logger.LogInformation("OrderChangeHandler, connectionIds: {connectionIds}",
                string.Join(CommonConstant.Comma, connectionIds));
            var orderChangeGrain = _clusterClient.GetGrain<IUserOrderChangeGrain>(address);
            var time = await orderChangeGrain.Get();
            var result = await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto
            {
                Address = address,
                Time = time
            });
            _logger.LogInformation(
                "OrderChangeHandler, address: {address}, time: {time}, pending: {depositCount1},{withdrawCount1}, success: {depositCount2},{withdrawCount2}, fail: {depositCount3},{withdrawCount3}",
                address, time, result?.Processing.DepositCount, result?.Processing.WithdrawCount,
                result?.Succeed.DepositCount, result?.Succeed.WithdrawCount, result?.Failed.DepositCount,
                result.Failed.WithdrawCount);
            foreach (var connectionId in connectionIds)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUserOrderRecords", result);
            }
        }
    }
}