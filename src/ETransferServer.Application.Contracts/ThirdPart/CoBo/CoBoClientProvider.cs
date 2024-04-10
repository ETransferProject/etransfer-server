using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ETransferServer.Common;
using ETransferServer.Common.Dtos;
using ETransferServer.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ThirdPart.CoBo;

public class CoBoClientProvider : ICoBoClientProvider, ISingletonDependency
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CoBoClientProvider> _logger;
    private readonly IOptionsSnapshot<SignatureServiceOption> _signatureOptions;
    private readonly CoBoOptions _coBoOptions;

    public CoBoClientProvider(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor,
        ILogger<CoBoClientProvider> logger, IOptionsSnapshot<SignatureServiceOption> options,
        IOptionsSnapshot<CoBoOptions> coBoOptions)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _signatureOptions = options;
        _coBoOptions = coBoOptions.Value;
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var client = await GetClientAsync(HttpMethod.Get, url, null);
        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        CheckResponse(response.StatusCode, url, content, null);

        return JsonConvert.DeserializeObject<T>(content);
    }

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers)
    {
        if (headers == null)
        {
            return await GetAsync<T>(url);
        }

        var client = await GetClientAsync(HttpMethod.Get, url, null);
        foreach (var keyValuePair in headers)
        {
            client.DefaultRequestHeaders.Add(keyValuePair.Key, keyValuePair.Value);
        }

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        CheckResponse(response.StatusCode, url, content, null);

        return JsonConvert.DeserializeObject<T>(content);
    }

    public async Task<T> PostAsync<T>(string url)
    {
        return await PostFormAsync<T>(url, null, null);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj)
    {
        return await PostFormAsync<T>(url, (Dictionary<string, string>)paramObj, null);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers)
    {
        return await PostFormAsync<T>(url, (Dictionary<string, string>)paramObj, headers);
    }

    public async Task<T> PostAsync<T>(string url, RequestMediaType requestMediaType, object paramObj,
        Dictionary<string, string> headers)
    {
        if (requestMediaType == RequestMediaType.Json)
        {
            return await PostJsonAsync<T>(url, paramObj, headers);
        }

        return await PostFormAsync<T>(url, (Dictionary<string, string>)paramObj, headers);
    }

    private async Task<T> PostJsonAsync<T>(string url, object paramObj, Dictionary<string, string> headers)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var requestInput = paramObj == null
            ? string.Empty
            : JsonConvert.SerializeObject(paramObj, Formatting.None, serializerSettings);

        var requestContent = new StringContent(
            requestInput,
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var client = GetClient();

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var response = await client.PostAsync(url, requestContent);
        var content = await response.Content.ReadAsStringAsync();
        CheckResponse(response.StatusCode, url, content, paramObj);

        return JsonConvert.DeserializeObject<T>(content);
    }

    private async Task<T> PostFormAsync<T>(string url, Dictionary<string, string> paramDic,
        Dictionary<string, string> headers)
    {
        var client = await GetClientAsync(HttpMethod.Post, url, paramDic);

        if (headers is { Count: > 0 })
        {
            foreach (var header in headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }

        var param = new List<KeyValuePair<string, string>>();
        if (paramDic is { Count: > 0 })
        {
            param.AddRange(paramDic.ToList());
        }

        var response = await client.PostAsync(url, new FormUrlEncodedContent(param));
        var content = await response.Content.ReadAsStringAsync();
        CheckResponse(response.StatusCode, url, content, paramDic);

        return JsonConvert.DeserializeObject<T>(content);
    }

    private HttpClient GetClient()
    {
        var client = _httpClientFactory.CreateClient(CoBoConstant.ClientName);
        return client;
    }

    private async Task<HttpClient> GetClientAsync(HttpMethod httpMethod, string url,
        Dictionary<string, string> paramDic)
    {
        var client = _httpClientFactory.CreateClient(CoBoConstant.ClientName);
        var headers = await GetHeadersAsync(httpMethod, url, paramDic);
        foreach (var header in headers)
        {
            client.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        return client;
    }

    private async Task<Dictionary<string, string>> GetHeadersAsync(HttpMethod httpMethod, string uri,
        Dictionary<string, string> paramDic)
    {
        var headers = new Dictionary<string, string>();
        var nonce = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        headers.Add(CoBoConstant.NonceName, nonce.ToString());

        var signatureContent = CoBoHelper.GetSignatureContent(httpMethod, uri, paramDic, nonce);

        var signDto = new SignDto
        {
            ApiKey = _coBoOptions.ApiKey,
            PlainText = signatureContent
        };
        _logger.LogInformation("GetHeadersAsync baseurl: ", _signatureOptions.Value.BaseUrl);

        var url = _signatureOptions.Value.BaseUrl.TrimEnd('/') + CommonConstant.ThirdPartSignUrl;
        var responseDto = await GetSignatureAsync(url, signDto);
        headers.Add(CoBoConstant.SignatureName, responseDto.Signature);

        return headers;
    }

    private void CheckResponse(HttpStatusCode statusCode, string url, string content, object param)
    {
        if (statusCode != HttpStatusCode.OK)
        {
            _logger.LogError(
                "Response not success, url:{url}, code:{code}, content: {content}, params:{param}",
                url, statusCode, content, param == null ? string.Empty : JsonConvert.SerializeObject(param));

            throw new UserFriendlyException(content, ((int)statusCode).ToString());
        }

        if (!CheckContent(content))
        {
            var errorResponse = JsonConvert.DeserializeObject<CoBoResponseErrorDto>(content);
            _logger.LogError(
                "Response content not success, url:{url}, content: {content}, params:{param}",
                url, content, param == null ? string.Empty : JsonConvert.SerializeObject(param));

            throw new UserFriendlyException(errorResponse.ErrorMessage, errorResponse.ErrorCode.ToString());
        }
    }

    private bool CheckContent(string content)
    {
        if (!content.Contains("success"))
        {
            return false;
        }

        var result = JObject.Parse(content);
        var success = Convert.ToBoolean(result["success"]);

        return success;
    }

    private async Task<SignResponseDto> GetSignatureAsync(string url, object paramObj)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        var requestInput = paramObj == null
            ? string.Empty
            : JsonConvert.SerializeObject(paramObj, Formatting.None, serializerSettings);

        var requestContent = new StringContent(
            requestInput,
            Encoding.UTF8,
            MediaTypeNames.Application.Json);

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(url, requestContent);
        var content = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != HttpStatusCode.OK)
        {
            _logger.LogError(
                "Response not success, url:{url}, code:{code}, content: {content}",
                url, response.StatusCode, content);

            throw new UserFriendlyException(content, response.StatusCode.ToString());
        }

        return JsonConvert.DeserializeObject<SignResponseDto>(content);
    }

    private class SignResponseDto
    {
        public string Signature { get; set; }
    }

    private class SignDto
    {
        public string ApiKey { get; set; }
        public string PlainText { get; set; }
    }
}