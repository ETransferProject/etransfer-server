using System.Threading.Tasks;
using ETransferServer.Common.HttpClient;
using ETransferServer.Dtos.TokenAccess;
using ETransferServer.Options;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.TokenAccess.Provider;

public interface ISymbolMarketProvider
{
    Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input);
    Task GetBindingIssueAsync(string id);
}

public class SymbolMarketProvider : ISymbolMarketProvider, ISingletonDependency
{
    private readonly TokenAccessOptions _tokenAccessOptions;
    private readonly IHttpProvider _httpProvider;
    
    public Task<string> PrepareBindingIssueAsync(PrepareBindIssueInput input)
    {
        throw new System.NotImplementedException();
    }

    public Task GetBindingIssueAsync(string id)
    {
        throw new System.NotImplementedException();
    }
}