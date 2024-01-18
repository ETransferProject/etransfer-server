using ETransferServer.Common.AElfSdk;
using Microsoft.Extensions.DependencyInjection;
using ETransferServer.Grains;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace ETransferServer;

[DependsOn(
    typeof(ETransferServerDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(ETransferServerApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(ETransferServerGrainsModule),
    typeof(AbpSettingManagementApplicationModule)
)]
public class ETransferServerApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<ETransferServerApplicationModule>(); });

        context.Services.AddHttpClient();
        context.Services.AddSingleton<SignatureProvider>();
    }
}