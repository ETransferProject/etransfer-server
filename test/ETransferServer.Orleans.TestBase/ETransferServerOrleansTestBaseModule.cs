using ETransferServer.ThirdPart.Exchange;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching;
using Volo.Abp.Modularity;
using Volo.Abp.ObjectMapping;

namespace ETransferServer;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpCachingModule),
    typeof(AbpAutoMapperModule),
    typeof(AbpObjectMappingModule),
    typeof(ETransferServerDomainModule)
)]
public class ETransferServerOrleansTestBaseModule : AbpModule
{
    private readonly ClusterFixture _fixture = new();

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton(_fixture);
        context.Services.AddSingleton<IClusterClient>(sp => _fixture.Cluster.Client);
        
        context.Services.AddTransient<IExchangeProvider, OkxProvider>();
        context.Services.AddTransient<IExchangeProvider, BinanceProvider>();
        context.Services.AddTransient<IExchangeProvider, CoinGeckoProvider>();
        context.Services.AddTransient<IExchangeProvider, GateIoProvider>();
        context.Services.AddTransient<IExchangeProvider, UniswapV3Provider>();
    }

}
