using ETransferServer.Dtos.User;
using ETransferServer.User;

namespace ETransferServer.Grains.Grain.Users;

public class UserAddressProvider : IUserAddressProvider 
{
     private readonly IUserAddressService _userAddressService;
    
    public UserAddressProvider(IUserAddressService userAddressService)
    {
        _userAddressService = userAddressService;
    }
    
    public async Task<UserAddressDto> GetUserUnAssignedAddressAsync(GetUserDepositAddressInput input)
    {
        return await _userAddressService.GetUnAssignedAddressAsync(input);
    }

    public async Task<List<string>> GetRemainingAddressListAsync()
    {
        return await _userAddressService.GetRemainingAddressListAsync();
    }

    public async Task<List<UserAddressDto>> GetAddressListAsync(List<string> addressList)
    {
        return await _userAddressService.GetAddressListAsync(addressList);
    }

    public async Task<List<UserAddressDto>> GetExpiredAddressListAsync()
    {
        return await _userAddressService.GetExpiredAddressListAsync();
    }

    public async Task<bool> BulkAddSync(List<UserAddressDto> dto)
    {
        return await _userAddressService.BulkAddOrUpdateAsync(dto);
    }

    public async Task<bool> UpdateSync(UserAddressDto dto)
    {
        return await _userAddressService.AddOrUpdateAsync(dto);
    }
}