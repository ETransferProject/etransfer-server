using System;
using System.Threading.Tasks;
using ETransferServer.Grains.Grain.Order.Deposit;
using ETransferServer.Grains.Grain.Timers;
using ETransferServer.Network.Dtos;
using ETransferServer.Options;
using Volo.Abp;
using Volo.Abp.Auditing;
using ETransferServer.Secp256k1;
using ETransferServer.ThirdPart.CoBo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.Extensions.Options;
using Orleans;

namespace ETransferServer.Cobo;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class CoboAppService : ETransferServerAppService, ICoboAppService
{
    private readonly ILogger<CoboAppService> _logger;
    private readonly Ecdsa _ecdsaClient;
    private readonly IClusterClient _clusterClient;
    private readonly ICoBoProvider _coBoProvider;
    private readonly IOptionsMonitor<CoBoOptions> _coBoOptions;

    public CoboAppService(Ecdsa ecdsaClient, ILogger<CoboAppService> logger, IClusterClient clusterClient, 
        ICoBoProvider coBoProvider, IOptionsMonitor<CoBoOptions> coBoOptions)
    {
        _ecdsaClient = ecdsaClient;
        _logger = logger;
        _clusterClient = clusterClient;
        _coBoProvider = coBoProvider;
        _coBoOptions = coBoOptions;
    }

    public async Task<TransactionNotificationResponse> TransactionNotificationAsync(string timestamp, string signature, string body)
    {
        var res = new TransactionNotificationResponse { };
        bool verifyResult = false;
        try
        {
            _logger.LogInformation("CocoService CoboCallBackAsync timestamp:{timestamp} signature:{signature} body:{body}", timestamp, signature, body);
            if (!string.IsNullOrEmpty(timestamp) && !string.IsNullOrEmpty(signature))
            {
                string content = body + "|" + timestamp;
                // var publicKeys = _coBoOptions.CurrentValue.PublicKey;
                // publicKeys = "038c3376e17f2739ad78a506741f01c284be2fbeac0c60cf61197d0f412445455e";
                verifyResult = await _ecdsaClient.VerifySignatureAsync(content, signature, _coBoOptions.CurrentValue.PublicKey);
                if (false == verifyResult)
                {
                    res.Success = false;
                }

                return res;
            }
        }
        catch (Exception e)
        {
            _logger.LogError("CocoService CoboCallBackAsync call back exception timestamp:{timestamp} e:{e}", timestamp, e.Message);
            res.Success = false;;
        }
        
        var orderInfo = JsonConvert.DeserializeObject<CoBoTransactionDto>(body);
        var id = new Guid(orderInfo.Id);
        var orderInfoGrain = await _clusterClient.GetGrain<IUserDepositRecordGrain>(id).GetAsync();
        if (orderInfoGrain.Success)
        {
            _logger.LogInformation("");
            res.Success = false;
            return res;
        }
        await _clusterClient.GetGrain<ICoBoDepositQueryTimerGrain>(id).GetDepositOrder(orderInfo);
        
        verifyResult = await CustomCheck(body);
        
        // add your checking policy
        // call grain
        res.Success = verifyResult;
        return res;
    }
    
    public async Task<bool> CustomCheck(string body)
    {
        // policy校验
        // 查询 UserDepositRecordGrain
        var orderInfo = JsonConvert.DeserializeObject<CoBoTransactionDto>(body);
        var id = new Guid(orderInfo.Id);
        var orderInfoGrain = await _clusterClient.GetGrain<IUserDepositRecordGrain>(id).GetAsync();
        if (orderInfoGrain.Success)
        {
            _logger.LogInformation("");
            return true;
        }
        var orderInfoNew = await _coBoProvider.GetTransaction(new TransactionIdRequestDto
        {
            Id = orderInfo.Id
        });
        var idNew = new Guid(orderInfoNew.Id);
        // 
        await _clusterClient.GetGrain<ICoBoDepositQueryTimerGrain>(id).CreateDepositOrder(orderInfoNew);
        return false;
    }
}