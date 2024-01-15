using Volo.Abp.AutoMapper;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace ETransferServer;

[DependsOn(
    typeof(AbpEventBusModule),
    typeof(ETransferServerApplicationModule),
    typeof(ETransferServerApplicationContractsModule),
    typeof(ETransferServerOrleansTestBaseModule),
    typeof(ETransferServerDomainTestModule)
)]
public class ETransferServerApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        base.ConfigureServices(context);
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<ETransferServerApplicationModule>(); });
        
    }
}