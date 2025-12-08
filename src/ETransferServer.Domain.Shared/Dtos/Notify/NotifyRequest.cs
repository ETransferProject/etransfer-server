using System.Collections.Generic;

namespace ETransferServer.Dtos.Notify;

public class NotifyRequest
{

    // Message Unique ID
    public string Id { get; set; }
    
    // Message Sender
    public string Sender { get; set; }

    // Message Recipient
    public List<string> TargetList { get; set; }
    
    // Message template
    public string Template { get; set; }
    
    // Message Parameters
    public Dictionary<string, string> Params { get; set; }

    // Message Extra Parameters
    public Dictionary<string, string> ExternalData { get; set; }

}