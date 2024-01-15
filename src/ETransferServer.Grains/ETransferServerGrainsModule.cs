using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;

namespace ETransferServer.Grains;

[DependsOn(
    typeof(AbpAutoMapperModule),
    typeof(ETransferServerApplicationContractsModule)
    )]
public class ETransferServerGrainsModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<ETransferServerGrainsModule>(); });
        
    }
}