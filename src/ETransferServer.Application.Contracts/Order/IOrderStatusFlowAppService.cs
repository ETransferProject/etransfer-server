using System.Threading.Tasks;
using ETransferServer.Dtos.Order;

namespace ETransferServer.Order;

public interface IOrderStatusFlowAppService
{
    Task<bool> AddOrUpdateAsync(OrderStatusFlowDto dto);
}