using System;
using System.Text;
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

    public CoboAppService(Ecdsa ecdsaClient, ILogger<CoboAppService> logger, IClusterClient clusterClient, 
        ICoBoProvider coBoProvider, IOptionsMonitor<CoBoOptions> coBoOptions)
    {
        _ecdsaClient = ecdsaClient;
        _logger = logger;
        _clusterClient = clusterClient;
        _coBoProvider = coBoProvider;
        _coBoOptions = coBoOptions;
    }

    public async Task<TransactionNotificationResponse> TransactionNotificationAsync(long timestamp, string signature, string body)
    {
        var res = new TransactionNotificationResponse { };
        bool verifyResult = false;
        try
        {
            _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{signature} body:{body}", timestamp, signature, body);
            if (!string.IsNullOrEmpty(signature))
            {
                timestamp = 1673530794575;
                var publicKeys = "032f45930f652d72e0c90f71869dfe9af7d713b1f67dc2f7cb51f9572778b9c876";
                signature =
                    "304402207f1a49a302bece956da7e0a3e9a77f0fb33ccc368ef2fa4dacac5c96d7d5f542022052dc95dbf0f409e290e38481b8b624ba2748ee08de6141be067ab80f5daf9b92";
                body = "{\"id\": \"20230110151203000381527000003478\", \"coin\": \"BTC\", \"display_code\": \"BTC\", \"description\": \"Bitcoin\", \"decimal\": 8, \"address\": \"3DPAyjXaYtBfZMbY8XVEUQShr2fYu9MMcg\", \"source_address\": \"bc1q46uh8ywdq22p7dhzkwxg49v5guvpx9kwgrjzlm\", \"side\": \"withdraw\", \"amount\": \"1000\", \"abs_amount\": \"0.00001\", \"txid\": \"c1a9b2d97c548cfd1c564d563d84ca136b247c96f0ad1fc7e0e2fb7cf05d74cb\", \"vout_n\": 0, \"request_id\": \"web_send_by_user_1272_1673332884996\", \"status\": \"success\", \"abs_cobo_fee\": \"0\", \"created_time\": 1673332885187, \"last_time\": 1673334723525, \"confirmed_num\": 3, \"request_created_time\": 1673332885187, \"tx_detail\": {\"txid\": \"c1a9b2d97c548cfd1c564d563d84ca136b247c96f0ad1fc7e0e2fb7cf05d74cb\", \"blocknum\": 771229, \"blockhash\": \"000000000000000000056f5be0c51de238fa793ca2ba76f81c7245842e1c0bbd\", \"fee\": 0, \"actualgas\": 1460, \"gasprice\": 1, \"hexstr\": \"\"}, \"source_address_detail\": \"bc1q46uh8ywdq22p7dhzkwxg49v5guvpx9kwgrjzlm\", \"memo\": \"\", \"confirming_threshold\": 3, \"fee_coin\": \"BTC\", \"fee_amount\": 40000, \"fee_decimal\": 8, \"type\": \"external\"}";
                string content = body + "|" + timestamp;
                // var publicKeys = _coBoOptions.CurrentValue.PublicKey;
                verifyResult = await _ecdsaClient.VerifySignatureAsync(content, signature, publicKeys);
                if (false == verifyResult)
                {
                    _logger.LogInformation("CocoService CoboCallBackAsync begin timestamp:{timestamp} signature:{signature} body:{body} verifyResult false", timestamp, signature, body);
                    res.Success = false;
                    // return res;
                }

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
        
        if (orderInfoGrain?.Data != null)
        {
            _logger.LogInformation("CocoService TransactionNotificationAsync order not exists, orderId:{orderId}", id);
            return res;
        }
        var orderInfoInGrain = await _clusterClient.GetGrain<ICoBoDepositQueryTimerGrain>(id).GetDepositOrder(orderInfo);
        
        verifyResult = await CustomCheck(body);
        _logger.LogInformation("CocoService CoboCallBackAsync end timestamp:{timestamp} signature:{signature} body:{body} orderInfoGrain.Success verifyResult: {verifyResult}", 
            timestamp, signature, body, verifyResult);
        
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
            var idNew = new Guid(orderInfoNew.Id);
            await _clusterClient.GetGrain<ICoBoDepositQueryTimerGrain>(id).CreateDepositOrder(orderInfoNew);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CoboService CustomCheck GetTransaction transaction error.");
        }
        return false;
    }
}