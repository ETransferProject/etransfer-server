using System.Threading.Tasks;
using Asp.Versioning;
using ETransferServer.Dtos.User;
using ETransferServer.User;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace ETransferServer.Controllers;

[RemoteService]
[Area("app")]
[ControllerName("User")]
[Route("api/etransfer/user")]
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
    
    [HttpGet("check-registration")]
    public async Task<RegistrationResult> CheckRegistration(GetRegistrationResultRequestDto requestDto)
    {
        return await _userAppService.CheckRegistrationAsync(requestDto);
    }
}