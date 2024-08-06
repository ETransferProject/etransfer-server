using System.Threading.Tasks;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using ETransferServer.Withdraw.Dtos;
using ETransferServer.WithdrawOrder.Dtos;
using Volo.Abp.Application.Services;

namespace ETransferServer.Order;

public interface IOrderWithdrawAppService : IApplicationService
{
    Task<bool> AddOrUpdateAsync(WithdrawOrderDto dto);
    Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request);
    Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(string version, GetWithdrawOrderRequestDto request);
    Task DoMonitorAsync(string network, decimal estimateFee, string feeSymbol, bool isNotify);
}