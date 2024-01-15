using System;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
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


    public async Task<bool> AddOrUpdateAsync(OrderStatusFlowDto dto)
    {
        try
        {
            await _depositOrderIndexRepository.AddOrUpdateAsync(
                _objectMapper.Map<OrderStatusFlowDto, OrderStatusFlow>(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError("Save depositOrderIndex fail: {id},{message}", dto.Id, ex.Message);
            return false;
        }

        return true;
    }
}