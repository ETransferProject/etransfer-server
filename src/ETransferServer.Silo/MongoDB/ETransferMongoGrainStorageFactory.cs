using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Storage;

namespace ETransferServer.Silo.MongoDB;

public static class ETransferMongoGrainStorageFactory
{
    public static IGrainStorage Create(IServiceProvider services, string name)
    {
        var optionsMonitor = services.GetRequiredService<IOptionsMonitor<MongoDBGrainStorageOptions>>();
        return ActivatorUtilities.CreateInstance<ETransferMongoGrainStorage>(services, optionsMonitor.Get(name));
    }
}