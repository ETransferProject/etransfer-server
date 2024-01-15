using ETransferServer.Common.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.Provider;

public interface IOrderStatusFlowProvider
{
    Task<bool> AddOrUpdate(Guid orderId, CommonResponseDto<OrderStatusFlowDto> resp);
}


public class OrderStatusFlowProvider : IOrderStatusFlowProvider, ISingletonDependency
{
    private readonly ILogger<OrderStatusFlowProvider> _logger;
    private readonly IOrderStatusFlowAppService _orderStatusFlowAppService;

    public OrderStatusFlowProvider(IOrderStatusFlowAppService orderStatusFlowAppService, ILogger<OrderStatusFlowProvider> logger)
    {
        _orderStatusFlowAppService = orderStatusFlowAppService;
        _logger = logger;
    }


    public async Task<bool> AddOrUpdate(Guid orderId, CommonResponseDto<OrderStatusFlowDto> resp)
    {
        if (resp.Success)
        {
        
            return await _orderStatusFlowAppService.AddOrUpdateAsync(resp.Data as OrderStatusFlowDto);
        }
        _logger.LogError("Deposit order status flow save failed, orderId={OrderId}, message={Msg}", orderId, 
            resp.Message);
        return false;
    }
}