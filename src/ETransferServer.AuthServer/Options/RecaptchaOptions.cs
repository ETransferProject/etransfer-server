namespace ETransferServer.Auth.Options;

public class RecaptchaOptions
{
    public string SecretKey { get; set; }
    public string BaseUrl { get; set; }
    public List<string> ChainIds { get; set; }
}