namespace ETransferServer.Auth.Options;

public class RecaptchaOptions
{
    public string SecretKey { get; set; }
    public string BaseUrl { get; set; }
    private List<string> _chainIds;
    public List<string> ChainIds
    {
        // Set default values to prevent situations where configuration is missing.
        get => _chainIds ??= new List<string> { "AELF", "tDVV", "tDVW" };
        set => _chainIds = value;
    }
}