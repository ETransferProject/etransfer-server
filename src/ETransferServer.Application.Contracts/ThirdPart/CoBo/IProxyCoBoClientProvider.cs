using System.Collections.Generic;
using System.Threading.Tasks;
using ETransferServer.Common.Dtos;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.ThirdPart.CoBo;

public interface IProxyCoBoClientProvider
{
    Task<T> GetAsync<T>(string url);
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers);
    Task<T> PostAsync<T>(string url);
    Task<T> PostAsync<T>(string url, object paramObj);
    Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers);
}

public class ProxyCoBoClientProvider : IProxyCoBoClientProvider, ISingletonDependency
{
    private readonly ICoBoClientProvider _clientProvider;

    public ProxyCoBoClientProvider(ICoBoClientProvider clientProvider)
    {
        _clientProvider = clientProvider;
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var response =
            await _clientProvider.GetAsync<CoBoResponseDto<T>>(url);

        return GetData(response);
    }

    public async Task<T> GetAsync<T>(string url, IDictionary<string, string> headers)
    {
        var response =
            await _clientProvider.GetAsync<CoBoResponseDto<T>>(url, headers);

        return GetData(response);
    }

    public async Task<T> PostAsync<T>(string url)
    {
        var response =
            await _clientProvider.GetAsync<CoBoResponseDto<T>>(url);

        return GetData(response);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj)
    {
        var response =
            await _clientProvider.PostAsync<CoBoResponseDto<T>>(url, paramObj);

        return GetData(response);
    }

    public async Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers)
    {
        var response =
            await _clientProvider.PostAsync<CoBoResponseDto<T>>(url, paramObj, headers);

        return GetData(response);
    }

    private T GetData<T>(CoBoResponseDto<T> response)
    {
        if (response.Success)
        {
            return response.Result;
        }

        throw new UserFriendlyException("get data fail");
    }
}