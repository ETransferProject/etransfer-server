using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AElf;
using ETransferServer.Common;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Dtos.Reconciliation;
using ETransferServer.Order;
using ETransferServer.Service.Info;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Identity;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Users;

namespace ETransferServer.Reconciliation;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class ReconciliationAppService : ApplicationService, IReconciliationAppService
{
    private readonly IClusterClient _clusterClient;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<OrderAppService> _logger;
    private readonly IInfoAppService _infoService;
    private readonly IOrderAppService _orderService;
    private readonly IdentityUserManager _userManager;

    public ReconciliationAppService(IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger,
        IInfoAppService infoService,
        IOrderAppService orderService, 
        IdentityUserManager userManager)
    {
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _infoService = infoService;
        _orderService = orderService;
        _userManager = userManager;
    }

    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        return await _infoService.GetNetworkOptionAsync();
    }

    public async Task<bool> ChangePasswordAsync(ChangePasswordRequestDto request)
    {
        try
        {
            var userId = CurrentUser.IsAuthenticated ? CurrentUser?.GetId() : null;
            if (!userId.HasValue || userId == Guid.Empty)
            {
                throw new UserFriendlyException("Invalid jwt.");
            }

            var identityUser = await _userManager.FindByIdAsync(userId.ToString());
            if (identityUser == null)
            {
                throw new UserFriendlyException("Invalid user.");
            }

            var password = Encoding.UTF8.GetString(ByteArrayHelper.HexStringToByteArray(request.NewPassword));
            if (!VerifyHelper.VerifyPassword(password))
            {
                throw new UserFriendlyException("Invalid password.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(identityUser);
            var result = await _userManager.ResetPasswordAsync(identityUser, token, password);
            if (!result.Succeeded)
            {
                throw new UserFriendlyException(string.Join(",", result.Errors.Select(e => e.Description)));
            }

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Change password failed.");
            return false;
        }
    }

    public async Task<OrderDetailDto> GetOrderRecordDetailAsync(string id)
    {
        return await _orderService.GetOrderRecordDetailAsync(id, true);
    }
}