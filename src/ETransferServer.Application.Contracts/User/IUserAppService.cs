using System.Threading.Tasks;
using ETransferServer.User.Dtos;

namespace ETransferServer.User;

public interface IUserAppService
{
    Task AddOrUpdateUserAsync(UserDto user);
    Task<UserDto> GetUserByIdAsync(string userId);
}