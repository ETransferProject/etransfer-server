using ETransferServer.Dtos.User;
using ETransferServer.User;

namespace ETransferServer.Grains.Grain.Users;

public interface IUserAddressProvider
{
    Task<UserAddressDto> GetUserUnAssignedAddressAsync(GetUserDepositAddressInput input);
    Task<List<string>> GetRemainingAddressListAsync();
    Task<List<UserAddressDto>> GetAddressListAsync(List<string> addressList);
    Task<List<UserAddressDto>> GetExpiredAddressListAsync(int expiredHour);
    Task<bool> BulkAddSync(List<UserAddressDto> dto);
    Task<bool> UpdateSync(UserAddressDto dto);
}