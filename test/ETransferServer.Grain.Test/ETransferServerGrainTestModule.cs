using ETransferServer.Grains;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectMapping;

namespace ETransferServer.Grain.Test;

[DependsOn(
    typeof(ETransferServerGrainsModule),
    typeof(ETransferServerDomainTestModule),
    typeof(ETransferServerDomainModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpObjectMappingModule)
)]
public class ETransferServerGrainTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IClusterClient>(sp => sp.GetService<ClusterFixture>().Cluster.Client);
        // context.Services.AddHttpClient();
        // context.Services.Configure<CoinGeckoOptions>(o => { o.CoinIdMapping["ELF"] = "aelf"; });
        // context.Services.Configure<CAAccountOption>(o =>
        // {
        //     o.CAAccountRequestInfoMaxLength = 100;
        //     o.CAAccountRequestInfoExpirationTime = 1;
        // });
    }
}