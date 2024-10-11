using System;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Order;
using ETransferServer.Orders;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Order;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class OrderStatusFlowAppService : IOrderStatusFlowAppService, ITransientDependency
{
    private readonly ILogger<OrderStatusFlowAppService> _logger;
    private readonly INESTRepository<OrderStatusFlow, Guid> _depositOrderIndexRepository;
    private readonly IObjectMapper _objectMapper;

    public OrderStatusFlowAppService(INESTRepository<OrderStatusFlow, Guid> depositOrderIndexRepository,
        IObjectMapper objectMapper, ILogger<OrderStatusFlowAppService> logger)
    {
        _depositOrderIndexRepository = depositOrderIndexRepository;
        _objectMapper = objectMapper;
        _logger = logger;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(ExceptionHelper),
        MethodName = nameof(ExceptionHelper.HandleException))]
    public async Task<bool> AddOrUpdateAsync(OrderStatusFlowDto dto)
    {
        await _depositOrderIndexRepository.AddOrUpdateAsync(
            _objectMapper.Map<OrderStatusFlowDto, OrderStatusFlow>(dto));
        return true;
    }
}