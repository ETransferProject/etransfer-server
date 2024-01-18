using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Deposit.Dtos;
using ETransferServer.Dtos.Order;
using ETransferServer.Models;
using Volo.Abp.Application.Services;

namespace ETransferServer.Order;

public interface IOrderDepositAppService: IApplicationService
{
    Task<bool> BulkAddOrUpdateAsync(List<DepositOrderDto> dtoList);
    Task<bool> AddOrUpdateAsync(DepositOrderDto dto);

    Task<GetDepositInfoDto> GetDepositInfoAsync(GetDepositRequestDto request);
}