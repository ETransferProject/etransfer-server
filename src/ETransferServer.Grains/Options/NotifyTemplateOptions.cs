using ETransferServer.Grains.Provider.Notify;

namespace ETransferServer.Grains.Options;

public class NotifyTemplateOptions
{
    public Dictionary<string, NotifyTemplate> Templates { get; set; }

}



public class NotifyTemplate
{
    public FeiShuGroupMessageTemplate FeiShuGroup { get; set; }
    
    // Add other notification methods templates here
}

public class FeiShuGroupMessageTemplate
{
    public string WebhookUrl { get; set; }
    public string Secret { get; set; }
    public string TitleTemplate { get; set; }
    public string Title { get; set; }
    public List<string> Contents { get; set; }
}