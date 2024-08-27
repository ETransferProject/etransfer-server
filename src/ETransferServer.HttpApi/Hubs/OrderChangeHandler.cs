using System.Linq;
using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Etos.Order;
using ETransferServer.Order;
using ETransferServer.User;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
            var userDto = await _userAppService.GetUserByIdAsync(eventData.Message.UserId.ToString());
            var address = userDto?.AddressInfos?.FirstOrDefault()?.Address;
            _logger.LogInformation("OrderChangeHandler, userId: {userId}, address: {address}", 
                eventData.Message.UserId, address);
            if (address.IsNullOrEmpty()) return;
            var connectionId = _hubConnectionProvider.GetUserConnection(address);
            _logger.LogInformation("OrderChangeHandler, connectionId: {connectionId}", connectionId);
            if (connectionId.IsNullOrEmpty()) return;
            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveUserOrderRecords", 
                await _orderAppService.GetUserOrderRecordListAsync(new GetUserOrderRecordRequestDto
            {
                Address = address
            }));
        }
    }
}