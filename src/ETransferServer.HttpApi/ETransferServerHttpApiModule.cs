using ETransferServer.Hubs;
using Localization.Resources.AbpUi;
using Microsoft.Extensions.DependencyInjection;
using ETransferServer.Localization;
using ETransferServer.Options;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.SignalR;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace ETransferServer;

[DependsOn(
    typeof(ETransferServerApplicationContractsModule),
    typeof(AbpAccountHttpApiModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpPermissionManagementHttpApiModule),
    typeof(AbpTenantManagementHttpApiModule),
    typeof(AbpFeatureManagementHttpApiModule),
    typeof(AbpSettingManagementHttpApiModule),
    typeof(AbpAspNetCoreSignalRModule)
    )]
public class ETransferServerHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(ETransferServerHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        ConfigureLocalization();
        context.Services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderChangeHandler>();
            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("MassTransit:RabbitMQ").Get<MassRabbitMqOptions>();
                cfg.Host(rabbitMqConfig.Host, rabbitMqConfig.Port, "/", h =>
                {
                    h.Username(rabbitMqConfig.UserName);
                    h.Password(rabbitMqConfig.Password);
                });
        
                cfg.ReceiveEndpoint(rabbitMqConfig.ClientQueueName, e =>
                {
                    e.ConfigureConsumer<OrderChangeHandler>(ctx);
                });
            });
        });
        context.Services.AddSignalR().AddStackExchangeRedis(configuration["Redis:Configuration"],
            options => { options.Configuration.ChannelPrefix = "ETransferServer"; });
    }

    private void ConfigureLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<ETransferServerResource>()
                .AddBaseTypes(
                    typeof(AbpUiResource)
                );
        });
    }
}

