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
    Task<bool> SaveTransferOrderInfoAsync(string orderId, GetTransferOrderInfoRequestDto request);
    Task<GetWithdrawInfoDto> GetWithdrawInfoAsync(GetWithdrawListRequestDto request, string version = null);
    Task<GetTransferInfoDto> GetTransferInfoAsync(GetTransferListRequestDto request, string version = null);
    Task<CreateWithdrawOrderDto> CreateWithdrawOrderInfoAsync(GetWithdrawOrderRequestDto request, string version = null, bool isTransfer = false);
    Task<CreateTransferOrderDto> CreateTransferOrderInfoAsync(GetTransferOrderRequestDto request, string version = null);
    Task DoMonitorAsync(string network, decimal estimateFee, string feeSymbol, bool isNotify);
}