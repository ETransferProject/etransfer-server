using ETransferServer.Dtos.Order;
using ETransferServer.Order;

namespace ETransferServer.Grains.Grain.Order.Withdraw;

public interface IUserWithdrawProvider
{
    Task<bool> AddOrUpdateSync(WithdrawOrderDto dto);
}

public class UserWithdrawProvider : IUserWithdrawProvider 
{
     private readonly IOrderWithdrawAppService _orderWithdrawAppService;
    
    public UserWithdrawProvider(IOrderWithdrawAppService orderWithdrawAppService)
    {
        _orderWithdrawAppService = orderWithdrawAppService;
    }

    public async Task<bool> AddOrUpdateSync(WithdrawOrderDto dto)
    {
        return await _orderWithdrawAppService.AddOrUpdateAsync(dto);
    }
}