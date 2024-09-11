using System;
using System.Threading.Tasks;
using ETransferServer.Dtos.Info;
using ETransferServer.Dtos.Order;
using ETransferServer.Order;
using ETransferServer.Service.Info;
using Microsoft.Extensions.Logging;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
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

    public ReconciliationAppService(IClusterClient clusterClient,
        IObjectMapper objectMapper,
        ILogger<OrderAppService> logger,
        IInfoAppService infoService,
        IOrderAppService orderService)
    {
        _clusterClient = clusterClient;
        _objectMapper = objectMapper;
        _logger = logger;
        _infoService = infoService;
        _orderService = orderService;
    }

    public async Task<GetTokenOptionResultDto> GetNetworkOptionAsync()
    {
        return await _infoService.GetNetworkOptionAsync();
    }

    public async Task<OrderDetailDto> GetOrderRecordDetailAsync(string id)
    {
        return await _orderService.GetOrderRecordDetailAsync(id, true);
    }
}