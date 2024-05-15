using System.Threading.Tasks;
using ETransferServer.Grains.Grain.Swap;
using ETransferServer.Swap;
using Orleans;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;

namespace ETransferServer.ChainsClient;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class TransactionTestAppService : ApplicationService, ITransactionTestAppService
{
    private readonly IClusterClient _clusterClient;

    public TransactionTestAppService(IClusterClient clusterClient)
    {
        _clusterClient = clusterClient;
    }

    public async Task<long> GetTransactionTimeAsync(string network, string blockHash, string transactionId)
    {
        var grain = _clusterClient.GetGrain<IEvmTransactionGrain>(blockHash);
        var result = await grain.GetTransactionTimeAsync(network,blockHash,transactionId);
        if (result.Success)
        {
            return result.Data;
        }
        return 0;
    }
}