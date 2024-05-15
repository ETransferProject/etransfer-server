using ETransferServer.ChainsClient.Evm;
using ETransferServer.ChainsClient.Solana;
using ETransferServer.ChainsClient.Tron;
using ETransferServer.Common.AElfSdk;
using ETransferServer.Common.ChainsClient;
using ETransferServer.Common.GraphQL;
using Microsoft.Extensions.DependencyInjection;
using ETransferServer.Grains;
using ETransferServer.GraphQL;
using Solnet.Rpc;
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
        context.Services.AddSingleton<IGraphQLClientFactory, GraphQLClientFactory>();
        context.Services.AddSingleton<IBlockchainClientFactory<Nethereum.Web3.Web3>, EvmClientFactory>();
        context.Services.AddSingleton<IBlockchainClientFactory<IRpcClient>, SolanaClientFactory>();
        context.Services.AddTransient<IBlockchainClientProvider, EvmClientProvider>();
        context.Services.AddTransient<IBlockchainClientProvider, TronClientProvider>();
        context.Services.AddTransient<IBlockchainClientProvider, SolanaClientProvider>();
    }
}