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
    Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request, string version = null);
    Task<GetWithdrawInfoDto> GetTransferInfoAsync(GetTransferListRequestDto request, string version = null);
    Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(GetWithdrawOrderRequestDto request, string version = null);
    Task DoMonitorAsync(string network, decimal estimateFee, string feeSymbol, bool isNotify);
}