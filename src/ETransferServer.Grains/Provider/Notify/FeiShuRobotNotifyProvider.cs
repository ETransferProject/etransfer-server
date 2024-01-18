using ETransferServer.Common;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.Notify;
using ETransferServer.Grains.Options;
using ETransferServer.Notify;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ETransferServer.Grains.Provider.Notify;

public class FeiShuRobotNotifyProvider : INotifyProvider
{
    private ILogger<FeiShuRobotNotifyProvider> _logger;
    private readonly HttpProvider _httpProvider;
    private readonly IOptionsMonitor<NotifyTemplateOptions> _notifyTemplateOptions;

    public FeiShuRobotNotifyProvider(HttpProvider httpProvider,
        IOptionsMonitor<NotifyTemplateOptions> notifyTemplateOptions)
    {
        _httpProvider = httpProvider;
        _notifyTemplateOptions = notifyTemplateOptions;
    }


    public NotifyTypeEnum NotifyType()
    {
        return NotifyTypeEnum.FeiShuGroup;
    }

    /// <summary>
    ///     InvokeAsync FeiShu robot webhook url
    /// </summary>
    /// <param name="notifyRequest">
    ///     notifyRequest.Sender should be EMPTY
    ///     notifyRequest.TargetList should be EMPTY
    ///     notifyRequest.Template = Template id 
    ///     notifyRequest.Param = FeiShu robot webhook body
    /// </param>
    /// <returns></returns>
    public async Task<bool> SendNotifyAsync(NotifyRequest notifyRequest)
    {
        var templateExists =
            _notifyTemplateOptions.CurrentValue.Templates.TryGetValue(notifyRequest.Template, out var templates);
        AssertHelper.IsTrue(templateExists, "Template {} not found", notifyRequest.Template);
        AssertHelper.NotNull(templates.FeiShuGroup, "FeiShuGroup template not fount, template:{}",
            notifyRequest.Template);

        var notifyContent = StringHelper.ReplaceObjectWithDict(templates.FeiShuGroup, notifyRequest.Params);
        var cardMessage = FeiShuMessageBuilder.CardMessageBuilder()
            .WithSignature(notifyContent.Secret)
            .WithTitle(notifyContent.Title, notifyContent.TitleTemplate)
            .AddMarkdownContents(notifyContent.Contents)
            .Build();

        var resp = await _httpProvider.InvokeAsync<FeiShuRobotResponse<Empty>>(HttpMethod.Post, notifyContent.WebhookUrl,
            body: JsonConvert.SerializeObject(cardMessage, HttpProvider.DefaultJsonSettings));
        return resp.Success;
    }
}

public class FeiShuRobotResponse<T>
{
    public int Code { get; set; }
    public T Data { get; set; }
    public string Msg { get; set; }
    public bool Success => Code == 0;
}