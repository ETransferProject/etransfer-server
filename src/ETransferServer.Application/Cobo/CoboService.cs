using System;
using System.Text;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Network.Dtos;
using ETransferServer.Options;
using Volo.Abp;
using Volo.Abp.Auditing;
using ETransferServer.ThirdPart.CoBo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Orleans;

namespace ETransferServer.Cobo;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class CoboAppService : ETransferServerAppService, ICoboAppService
{
    private readonly ILogger<CoboAppService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsSnapshot<CoBoOptions> _coBoOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CoboAppService(ILogger<CoboAppService> logger, IClusterClient clusterClient, 
        ICoBoProvider coBoProvider, IOptionsSnapshot<CoBoOptions> coBoOptions, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _clusterClient = clusterClient;
        _coBoProvider = coBoProvider;
        _coBoOptions = coBoOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TransactionNotificationResponse> TransactionNotificationAsync(long timestamp, string signature, string body)
    {
        var res = new TransactionNotificationResponse { };
        bool verifyResult = false;
        try
        {
            if (RequestIpIllegal())
            {
                return res;
            }
            _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{signature} body:{body}", timestamp, signature, body);
        }
        catch (Exception e)
        {
            _logger.LogError("CocoService CoboCallBackAsync call back exception timestamp:{timestamp} e:{e}", timestamp, e.Message);
            res.Success = false;;
        }
        verifyResult = await CustomCheckAndAdd(body);
        _logger.LogInformation("CocoService CoboCallBackAsync end timestamp:{timestamp} signature:{signature} body:{body} orderInfoGrain.Success verifyResult: {verifyResult}", 
            timestamp, signature, body, verifyResult);
        res.Success = verifyResult;
        return res;
    }

    public bool RequestIpIllegal()
    {
        if (_httpContextAccessor.HttpContext != null)
        {
            var ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress;
            if (ipAddress == null)
            {
                _logger.LogInformation("CocoService RequestIpIllegal ipAddress null ");
                return true;
            }
            if (ipAddress.IsIPv4MappedToIPv6)
            {
                ipAddress = ipAddress.MapToIPv4();
            }
        
            var coboIps = _coBoOptions.Value.CoboIps;
            if (coboIps.Contains(ipAddress.ToString()))
            {
                _logger.LogInformation("CocoService RequestIpIllegal success ip{ip}", ipAddress.ToString());
                return false;
            }
            _logger.LogInformation("CocoService RequestIpIllegal ip not in whitelist ip{ip}", ipAddress.ToString());
        }
        _logger.LogInformation("CocoService RequestIpIllegal success _httpContextAccessor.HttpContext null ");
        return true;
    }

    public async Task<bool> CustomCheckAndAdd(string body)
    {
        var orderInfo = JsonConvert.DeserializeObject<CoBoTransactionDto>(body);
        var id = new Guid(orderInfo.Id);
        var orderInfoGrain = await _clusterClient.GetGrain<IUserDepositRecordGrain>(id).GetAsync();
        if (orderInfoGrain?.Data != null)
        {
            _logger.LogWarning("CocoService CustomCheck order not exists, orderId:{orderId}", id);
            return true;
        }
        try
        {
            var orderInfoNew = await _coBoProvider.GetTransactionAsync(new TransactionIdRequestDto
                    {
                        Id = orderInfo.Id
                    });
            if (orderInfoNew == null)
            {
                _logger.LogWarning("CoboService CustomCheckAndAdd order {Id} stream error, request to transaction fail.", orderInfo.Id);
                return false;
            }
            _logger.LogWarning("CoboService CustomCheckAndAdd order {Id}, request to transaction orderInfoNew{orderInfoNew}", orderInfo.Id, JsonConvert.SerializeObject(orderInfoNew));
            var idNew = new Guid(orderInfoNew.Id);
            await _clusterClient.GetGrain<ICoBoDepositQueryTimerGrain>(id).CreateDepositOrder(orderInfoNew);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CoboService CustomCheck GetTransaction transaction error e:{e}", e.Message);
        }
        return false;
    }
}