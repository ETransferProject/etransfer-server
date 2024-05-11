using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ETransferServer.Common;
using ETransferServer.Common.GraphQL;
using ETransferServer.Options;
using Volo.Abp.Account;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectExtending;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace ETransferServer;

[DependsOn(
    typeof(ETransferServerDomainSharedModule),
    typeof(AbpAccountApplicationContractsModule),
    typeof(AbpFeatureManagementApplicationContractsModule),
    typeof(AbpIdentityApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationContractsModule),
    typeof(AbpSettingManagementApplicationContractsModule),
    typeof(AbpTenantManagementApplicationContractsModule),
    typeof(AbpObjectExtendingModule)
)]
public class ETransferServerApplicationContractsModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        ETransferServerDtoExtensions.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<AbpAutoMapperOptions>(options => { options.AddMaps<ETransferServerApplicationContractsModule>(); });

        context.Services.AddHttpClient();
        AddCoBoClient(context, configuration);
        Configure<CoBoOptions>(configuration.GetSection("CoBo"));
    }

    private void AddCoBoClient(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var coBoOptions = configuration.GetSection("CoBo").Get<CoBoOptions>();
        if (coBoOptions == null || coBoOptions.BaseUrl.IsNullOrWhiteSpace())
        {
            return;
        }

        context.Services.AddHttpClient(CoBoConstant.ClientName, httpClient =>
        {
            httpClient.BaseAddress = new Uri(coBoOptions.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(coBoOptions.Timeout);
            
            if (!coBoOptions.ApiKey.IsNullOrWhiteSpace())
            {
                httpClient.DefaultRequestHeaders.Add(
                    CoBoConstant.ApiKeyName, coBoOptions.ApiKey);
            }
        });
    }
}