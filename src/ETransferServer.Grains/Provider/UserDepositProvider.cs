using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.Grain.Order.Deposit;

public interface IUserDepositProvider
{
    Task<bool> AddOrUpdateSync(DepositOrderDto dto);
    Task<bool> ExistSync(DepositOrderDto dto);
}

public class UserDepositProvider : IUserDepositProvider, ISingletonDependency 
{
     private readonly IOrderDepositAppService _orderDepositAppService;
    
    public UserDepositProvider(IOrderDepositAppService orderDepositAppService)
    {
        _orderDepositAppService = orderDepositAppService;
    }

    public async Task<bool> AddOrUpdateSync(DepositOrderDto dto)
    {
        return await _orderDepositAppService.AddOrUpdateAsync(dto);
    }
    
    public async Task<bool> ExistSync(DepositOrderDto dto)
    {
        return await _orderDepositAppService.ExistSync(dto);
    }
}