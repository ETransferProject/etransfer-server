using System.Net.Http;
using System.Threading.Tasks;
using AElf.Types;
using ETransferServer.Common.HttpClient;
using ETransferServer.Options;
using ETransferServer.Samples.HttpClient;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ETransferServer.Common.AElfSdk;

public class SignatureProvider
{

    private ApiInfo SignatureUri => new (HttpMethod.Post, _signatureServiceOption.CurrentValue.SignatureUri);
    
    private readonly IOptionsMonitor<SignatureServiceOption> _signatureServiceOption;
    private readonly IHttpProvider _httpProvider;

    public SignatureProvider(IOptionsMonitor<SignatureServiceOption> signatureServiceOption, IHttpProvider httpProvider)
    {
        _signatureServiceOption = signatureServiceOption;
        _httpProvider = httpProvider;
    }
    
    public async Task<string> GetTransactionSignature(string account, Hash transactionId)
    {

        var signResp = await _httpProvider.InvokeAsync<SignResponseDto>(_signatureServiceOption.CurrentValue.BaseUrl, SignatureUri,
            body: JsonConvert.SerializeObject(new SendSignatureDto
            {
                Account = account,
                HexMsg = transactionId.ToHex()
            }, HttpProvider.DefaultJsonSettings)
        );
        AssertHelper.NotNull(signResp, "Empty signature response");

        return signResp.Signature;
    }
    

    private class SendSignatureDto
    {
        public string Account { get; set; }
        public string HexMsg { get; set; }
    }

    private class SignResponseDto
    {
        public string Signature { get; set; }
    }
    
}