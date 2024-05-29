using System.Threading.Tasks;
using ETransferServer.Dtos.User;
using ETransferServer.User;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("User")]
[Route("api/app/user")]
public class UserController
{
    private readonly IUserAppService _userAppService;
    
    public UserController(IUserAppService userAppService)
    {
        _userAppService = userAppService;
    }
    
    [HttpGet("check-eoa-registration")]
    public async Task<EoaRegistrationResult> CheckEoaRegistration(GetEoaRegistrationResultRequestDto requestDto)
    {
        return await _userAppService.CheckEoaRegistrationAsync(requestDto);
    }
}