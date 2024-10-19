using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AElf.ExceptionHandler;
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
public partial class TransactionAppService : ETransferServerAppService, ITransactionAppService
{
    private readonly INESTRepository<OrderIndex, Guid> _orderIndexRepository;
    private readonly ILogger<TransactionAppService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IOptionsSnapshot<DepositInfoOptions> _depositInfoOptions;
    private readonly IOptionsSnapshot<CoBoOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TransactionAppService(INESTRepository<OrderIndex, Guid> orderIndexRepository,
        ILogger<TransactionAppService> logger, 
        IClusterClient clusterClient,
        IOptionsSnapshot<DepositInfoOptions> depositInfoOptions, 
        IOptionsSnapshot<CoBoOptions> options, 
        IHttpContextAccessor httpContextAccessor)
    {
        _orderIndexRepository = orderIndexRepository;
        _logger = logger;
        _clusterClient = clusterClient;
        _depositInfoOptions = depositInfoOptions;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
    }

    [ExceptionHandler(typeof(Exception), TargetType = typeof(TransactionAppService),
        MethodName = nameof(HandleExceptionAsync))]
    public async Task<string> TransactionNotificationAsync(string timestamp, string signature)
    {
        var body = await GetBodyAsync();
        _logger.LogInformation(
            "receive transaction callback, timestamp:{timestamp}, signature:{signature}, body:{body}",
            timestamp, signature, body);

        var handleResult = false;
        var content = $"{body}|{timestamp}";
        var verifyResult =
            SignatureHelper.VerifySignature(content, signature, publicKey: _options.Value.PublicKey);
        AssertHelper.IsTrue(verifyResult, reason: "valid signature fail.");
        // return NotificationEnum.Deny.ToString().ToLower();
        
        var notificationGrain = _clusterClient.GetGrain<ITransactionNotificationGrain>(Guid.NewGuid());
        handleResult = await notificationGrain.TransactionNotification(timestamp, signature, body);
        AssertHelper.IsTrue(handleResult, reason: "handle transaction fail.");
        return NotificationEnum.Ok.ToString().ToLower();
    }

    public async Task<TransactionCheckResult> TransactionCheckAsync(GetTransactionCheckRequestDto request)
    {
        var mustQuery = new List<Func<QueryContainerDescriptor<OrderIndex>, QueryContainer>>();
        mustQuery.Add(q => q.Term(i =>
            i.Field(f => f.OrderType).Value(OrderTypeEnum.Deposit.ToString())));
        if (!request.Type.HasValue)
        {
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.FromTransfer.Symbol).Value(CommonConstant.Symbol.USDT)));
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.ToTransfer.Symbol).Value(CommonConstant.Symbol.SGR)));
        }
        else if (_depositInfoOptions.Value.TxPairType.ContainsKey(request.Type.Value.ToString()))
        {
            var tokenPairs = _depositInfoOptions.Value.TxPairType[request.Type.Value.ToString()]
                .Split(CommonConstant.Underline);
            if (tokenPairs.Length <= 1)
            {
                return new TransactionCheckResult
                {
                    Result = false
                };
            }
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.FromTransfer.Symbol).Value(tokenPairs[0])));
            mustQuery.Add(q => q.Term(i =>
                i.Field(f => f.ToTransfer.Symbol).Value(tokenPairs[1])));
        }
        else
        {
            return new TransactionCheckResult
            {
                Result = false
            };
        }

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