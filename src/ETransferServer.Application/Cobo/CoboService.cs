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
using ETransferServer.Secp256k1;
using ETransferServer.ThirdPart.CoBo;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ETransferServer.ThirdPart.CoBo.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
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
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CoboAppService(Ecdsa ecdsaClient, ILogger<CoboAppService> logger, IClusterClient clusterClient, 
        ICoBoProvider coBoProvider, IOptionsMonitor<CoBoOptions> coBoOptions, IHttpContextAccessor httpContextAccessor)
    {
        _ecdsaClient = ecdsaClient;
        _logger = logger;
        _clusterClient = clusterClient;
        _coBoProvider = coBoProvider;
        _coBoOptions = coBoOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<TransactionNotificationResponse> TransactionNotificationAsync(long timestamp, string signature, string body)
    {
        var res = new TransactionNotificationResponse { };
        // test apollo
        var publicKeys = _coBoOptions.CurrentValue.PublicKey;
        _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{publicKeys} body:{body}", timestamp, publicKeys, body);
        res.Exception = publicKeys;
        return res;
        // 
        bool verifyResult = false;
        try
        {
            if (RequestIpIllegal())
            {
                return res;
            }
            _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{signature} body:{body}", timestamp, signature, body);
            // if (!string.IsNullOrEmpty(signature))
            // {
            //     // timestamp = 1673530794575;
            //     // var publicKeys = "032f45930f652d72e0c90f71869dfe9af7d713b1f67dc2f7cb51f9572778b9c876";
            //     // signature = "304402207f1a49a302bece956da7e0a3e9a77f0fb33ccc368ef2fa4dacac5c96d7d5f542022052dc95dbf0f409e290e38481b8b624ba2748ee08de6141be067ab80f5daf9b92";
            //     // body = "{\"Id\":\"20240321192739000163526000000738\",\"Coin\":\"SETH_SGR-1\",\"display_code\":\"SGR\",\"Description\":\"\",\"Decimal\":8,\"Address\":\"0x2f3a3326095dec3b9507b4881df800bf7968320e\",\"source_address\":\"0x1736d5684080703b2052f0f563c2bc48c00fe60d\",\"Side\":\"deposit\",\"Amount\":\"173761171\",\"abs_amount\":\"1.73761171\",\"txid\":\"0x48b6c1c7e5c0b560baf56cb02e815cc8371d3d49fa0b175d37a113b61ddef6d9\",\"vout_n\":0,\"request_id\":null,\"Status\":\"success\",\"created_time\":1711020459000,\"last_time\":1711020459000,\"Remark\":\"\",\"confirmed_num\":68,\"confirming_threshold\":64,\"abs_cobo_fee\":\"0\",\"fee_coin\":null,\"fee_amount\":null,\"fee_decimal\":null}";
            //     // var publicKeys = _coBoOptions.CurrentValue.PublicKey;
            //     string content = body + "|" + timestamp;
            //     verifyResult = await _ecdsaClient.VerifySignatureAsync(content, signature, _coBoOptions.CurrentValue.PublicKey);
            //     if (false == verifyResult)
            //     {
            //         _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{signature} body:{body} verifyResult false", timestamp, signature, body);
            //         res.Success = false;
            //         return res;
            //     }
            //
            // }
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
                return true;
            }
            if (ipAddress.IsIPv4MappedToIPv6)
            {
                ipAddress = ipAddress.MapToIPv4();
            }
        
            var coboIps = _coBoOptions.CurrentValue.CoboIps;
            if (coboIps.Contains(ipAddress.ToString()))
            {
                return false;
            }
        }

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
            var orderInfoNew = await _coBoProvider.GetTransaction(new TransactionIdRequestDto
                    {
                        Id = orderInfo.Id
                    });
            if (orderInfoNew == null)
            {
                _logger.LogWarning("CoboService CustomCheckAndAdd order {Id} stream error, request to transaction fail.", orderInfo.Id);
                return false;
            }
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