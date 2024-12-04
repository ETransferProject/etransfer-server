using System.Threading.Tasks;
using ETransferServer.Common.HttpClient;
using ETransferServer.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.TokenAccess.Provider;

public interface ILiquidityDataProvider
{
    Task<string> GetTokenTvlAsync(string symbol);
}

public class LiquidityDataProvider : ILiquidityDataProvider, ISingletonDependency
{
    private readonly TokenAccessOptions _tokenAccessOptions;
    private readonly IHttpProvider _httpProvider;
    public Task<string> GetTokenTvlAsync(string symbol)
    {
        throw new System.NotImplementedException();
    }
}