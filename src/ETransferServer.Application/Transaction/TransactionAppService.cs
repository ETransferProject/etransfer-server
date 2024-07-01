using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AElf.Indexing.Elasticsearch;
using ETransferServer.Common;
using ETransferServer.Dtos.Transaction;
using ETransferServer.Grains.Grain.Worker.Transaction;
using ETransferServer.Options;
using ETransferServer.Orders;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.Auditing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;
using Orleans;

namespace ETransferServer.Service.Transaction;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TransactionAppService : ETransferServerAppService, ITransactionAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly ILogger<TransactionAppService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IOptionsSnapshot<CoBoOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TransactionAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        ILogger<TransactionAppService> logger, 
        IClusterClient clusterClient,
        IOptionsSnapshot<CoBoOptions> options, 
        IHttpContextAccessor httpContextAccessor)
    {
        _orderIndexRepository = orderIndexRepository;
        _logger = logger;
        _clusterClient = clusterClient;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> TransactionNotificationAsync(string timestamp, string signature)
    {
        var body = await GetBodyAsync();
        _logger.LogInformation(
            "receive transaction callback, timestamp:{timestamp}, signature:{signature}, body:{body}",
            timestamp, signature, body);

        var handleResult = false;
        try
        {
            var content = $"{body}|{timestamp}";
            var verifyResult =
                SignatureHelper.VerifySignature(content, signature, publicKey: _options.Value.PublicKey);
            AssertHelper.IsTrue(verifyResult, reason: "valid signature fail.");

            var notificationGrain = _clusterClient.GetGrain<ITransactionNotificationGrain>(Guid.NewGuid());
            handleResult = await notificationGrain.TransactionNotification(timestamp, signature, body);
            AssertHelper.IsTrue(handleResult, reason: "handle transaction fail.");
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "handle receive transaction callback error, timestamp:{timestamp}, signature:{signature}, body:{body}",
                timestamp, signature, body);
            handleResult = false;
        }

        return handleResult ? NotificationEnum.Ok.ToString().ToLower() : NotificationEnum.Deny.ToString().ToLower();
    }

    public async Task<TransactionCheckResult> TransactionCheckAsync(GetTransactionCheckRequestDto request)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Deposit.ToString())));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.FromTransfer.Symbol).Value(CommonConstant.Symbol.USDT)));
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.ToTransfer.Symbol).Value(CommonConstant.Symbol.SGR)));
        mustQuery.Add(q => q.Terms(i =>
            i.Field(f => f.Status).Terms(new List<string>
            {
                OrderStatusEnum.ToTransferConfirmed.ToString(),
                OrderStatusEnum.Finish.ToString()
            })));
        
        if (!string.IsNullOrWhiteSpace(request.Address))
        {
            var addressSplit = request.Address.Trim().ToLower().Split(CommonConstant.Underline, StringSplitOptions.RemoveEmptyEntries);
            if (addressSplit.Length == 3 && addressSplit[0].Equals(CommonConstant.Symbol.Elf, StringComparison.OrdinalIgnoreCase))
            {
                mustQuery.Add(q => q.Term(i =>
                    i.Field(f => f.ToTransfer.ChainId).Value(addressSplit[2]).CaseInsensitive()));
                mustQuery.Add(q => q.Term(i =>
                    i.Field(f => f.ToTransfer.ToAddress).Value(addressSplit[1]).CaseInsensitive()));
            }
            else if (addressSplit.Length == 1)
            {
                mustQuery.Add(q => q.Bool(i => i.Should(
                    s => s.Term(w =>
                        w.Field(f => f.FromTransfer.FromAddress).Value(request.Address.Trim()).CaseInsensitive()),
                    s => s.Term(w =>
                        w.Field(f => f.ToTransfer.ToAddress).Value(request.Address.Trim()).CaseInsensitive()))));
            }
            else
            {
                return new TransactionCheckResult
                {
                    Result = false
                };
            }
        }
        QueryContainer Filter(QueryContainerDescriptor<OrderIndex> f) => f.Bool(b => b.Must(mustQuery));

        var countResponse = await _orderIndexRepository.CountAsync(Filter);
        return new TransactionCheckResult
        {
            Result = countResponse.Count > 0
        };
    }

    private async Task<string> GetBodyAsync()
    {
        var stream = new StreamReader(_httpContextAccessor.HttpContext.Request.Body);
        var body = await stream.ReadToEndAsync();
        stream.Close();
        return body;
    }
}