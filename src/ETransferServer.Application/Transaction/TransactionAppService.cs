using System;
using System.IO;
using System.Threading.Tasks;
using ETransferServer.Common;
using ETransferServer.Grains.Grain.Worker.Transaction;
using ETransferServer.Options;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.Auditing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace ETransferServer.Service.Transaction;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TransactionAppService : ETransferServerAppService, ITransactionAppService
{
    private readonly ILogger<TransactionAppService> _logger;
    private readonly IClusterClient _clusterClient;
    private readonly IOptionsSnapshot<CoBoOptions> _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TransactionAppService(ILogger<TransactionAppService> logger, IClusterClient clusterClient,
        IOptionsSnapshot<CoBoOptions> options, IHttpContextAccessor httpContextAccessor)
    {
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

    private async Task<string> GetBodyAsync()
    {
        var stream = new StreamReader(_httpContextAccessor.HttpContext.Request.Body);
        var body = await stream.ReadToEndAsync();
        stream.Close();
        return body;
    }
}