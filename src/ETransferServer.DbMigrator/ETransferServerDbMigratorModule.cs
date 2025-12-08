using ETransferServer.MongoDB;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Modularity;

namespace ETransferServer.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(ETransferServerMongoDbModule),
    typeof(ETransferServerApplicationContractsModule)
    )]
public class ETransferServerDbMigratorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);
    }
}
