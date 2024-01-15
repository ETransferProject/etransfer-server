using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Dtos.User;
using Volo.Abp.Application.Services;

namespace ETransferServer.User;

public interface IUserAddressService: IApplicationService
{
    Task<string> GetUserAddressAsync(GetUserDepositAddressInput input);
    Task<UserAddressDto> GetUnAssignedAddressAsync(GetUserDepositAddressInput input);
    Task<List<string>> GetRemainingAddressListAsync();
    Task<bool> BulkAddOrUpdateAsync(List<UserAddressDto> dtoList);
    Task<bool> AddOrUpdateAsync(UserAddressDto dto);
    Task<List<UserAddressDto>> GetAddressListAsync(List<string> addressList);
    Task<UserAddressDto> GetUnAssignedAddressAsync(string address);
}