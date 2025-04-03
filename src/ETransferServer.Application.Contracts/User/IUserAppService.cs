using System.Threading.Tasks;
using ETransferServer.Dtos.User;
using ETransferServer.User.Dtos;

namespace ETransferServer.User;

public interface IUserAppService
{
    Task AddOrUpdateUserAsync(UserDto user);
    Task<UserDto> GetUserByIdAsync(string userId);
    Task<UserDto> GetUserByAddressAsync(string address);
    Task<EoaRegistrationResult> CheckEoaRegistrationAsync(GetEoaRegistrationResultRequestDto requestDto);
    Task<RegistrationResult> CheckRegistrationAsync(GetRegistrationResultRequestDto requestDto);
}