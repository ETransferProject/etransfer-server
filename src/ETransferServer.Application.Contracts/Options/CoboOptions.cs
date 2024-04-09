using System.Collections.Generic;

namespace ETransferServer.Options;

public class CoBoOptions
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
    public int Timeout { get; set; } = 15;
    public int CoinExpireSeconds { get; set; } = 180;
    public string PublicKey { get; set; }
    public List<string> CoboIps { get; set; }
}