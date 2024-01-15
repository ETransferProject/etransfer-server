using System.Collections.Generic;
using System.Threading.Tasks;

namespace ETransferServer.ThirdPart.CoBo;

public interface ICoBoClientProvider
{
    Task<T> GetAsync<T>(string url);
    Task<T> GetAsync<T>(string url, IDictionary<string, string> headers);
    Task<T> PostAsync<T>(string url);
    Task<T> PostAsync<T>(string url, object paramObj);
    Task<T> PostAsync<T>(string url, object paramObj, Dictionary<string, string> headers);
}