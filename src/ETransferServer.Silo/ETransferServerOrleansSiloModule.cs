using ETransferServer.Common.AElfSdk;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ETransferServer.Common.HttpClient;
using ETransferServer.Grains;
using ETransferServer.Grains.Grain.Order.Withdraw;
using ETransferServer.Grains.Grain.Users;
using ETransferServer.Grains.Options;
using ETransferServer.Grains.Provider.Notify;
using ETransferServer.MongoDB;
using ETransferServer.Options;
using ETransferServer.ThirdPart.Exchange;
using ETransferServer.User;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.Tokens;

namespace ETransferServer.Silo;
[DependsOn(typeof(AbpAutofacModule),
    typeof(ETransferServerGrainsModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(ETransferServerMongoDbModule),
    typeof(ETransferServerApplicationModule)
)]
public class ETransferServerOrleansSiloModule : AbpModule
{
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<ChainOptions>(configuration.GetSection("Chains"));
        Configure<SignatureServiceOption>(configuration.GetSection("SignatureService"));
        Configure<TimerOptions>(configuration.GetSection("Timer"));
        Configure<DepositOptions>(configuration.GetSection("Deposit"));
        Configure<WithdrawOptions>(configuration.GetSection("Withdraw"));
        Configure<DepositAddressOptions>(configuration.GetSection("DepositAddress"));
        Configure<ETransferServer.Grains.Options.NetworkOptions>(configuration.GetSection("CoinNetworks"));
        Configure<WithdrawNetworkOptions>(configuration.GetSection("WithdrawNetwork"));
        Configure<NetWorkReflectionOptions>(configuration.GetSection("NetWorkReflection"));
        Configure<NotifyTemplateOptions>(configuration.GetSection("NotifyTemplates"));
        Configure<ExchangeOptions>(configuration.GetSection("Exchange"));
        Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
        
        context.Services.AddHostedService<ETransferServerHostedService>();
        context.Services.AddHttpClient();
        context.Services.AddSingleton<HttpProvider>();
        context.Services.AddSingleton<SignatureProvider>();
        context.Services.AddTransient<IUserAddressProvider, UserAddressProvider>();
        context.Services.AddTransient<IUserAppService, UserAppService>();
        context.Services.AddTransient<IUserWithdrawProvider, UserWithdrawProvider>();

        context.Services.AddTransient<INotifyProvider, FeiShuRobotNotifyProvider>();
        context.Services.AddTransient<IExchangeProvider, OkxProvider>();
        context.Services.AddTransient<IExchangeProvider, BinanceProvider>();
        context.Services.AddTransient<IExchangeProvider, CoinGeckoProvider>();

        ConfigureGraphQl(context, configuration);
    } 

    
    
    private void ConfigureGraphQl(ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }

    
}