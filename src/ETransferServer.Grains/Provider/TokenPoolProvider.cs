using ETransferServer.Dtos.Token;
using ETransferServer.Reconciliation;
using Volo.Abp.DependencyInjection;

namespace ETransferServer.Grains.Provider;

public interface ITokenPoolProvider
{
    Task<bool> AddOrUpdateSync(TokenPoolDto dto);
    Task<Tuple<Dictionary<string, string>, Dictionary<string, string>>> GetFeeListAsync(bool includeAll);
}

public class TokenPoolProvider : ITokenPoolProvider, ISingletonDependency 
{
     private readonly IReconciliationAppService _reconciliationAppService;
    
    public TokenPoolProvider(IReconciliationAppService reconciliationAppService)
    {
        _reconciliationAppService = reconciliationAppService;
    }

    public async Task<bool> AddOrUpdateSync(TokenPoolDto dto)
    {
        return await _reconciliationAppService.AddOrUpdateTokenPoolAsync(dto);
    }

    public async Task<Tuple<Dictionary<string, string>, Dictionary<string, string>>> GetFeeListAsync(bool includeAll)
    {
        return await _reconciliationAppService.GetFeeListAsync(includeAll);
    }
}