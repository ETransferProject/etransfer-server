using AElf.ExceptionHandler.Orleans.Extensions;
using AElf.OpenTelemetry;
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
using MassTransit;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using SwapInfosOptions = ETransferServer.Grains.Options.SwapInfosOptions;

namespace ETransferServer.Silo;
[DependsOn(typeof(AbpAutofacModule),
    typeof(ETransferServerGrainsModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(ETransferServerMongoDbModule),
    typeof(ETransferServerApplicationModule),
    typeof(OpenTelemetryModule)
)]
public class ETransferServerOrleansSiloModule : AbpModule
{
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<ChainOptions>(configuration.GetSection("Chains"));
        Configure<SignatureServiceOption>(configuration.GetSection("SignatureService"));
        Configure<SyncStateServiceOption>(configuration.GetSection("SyncStateService"));
        Configure<TimerOptions>(configuration.GetSection("Timer"));
        Configure<ETransferServer.Grains.Options.TokenAccessOptions>(configuration.GetSection("TokenAccess"));
        Configure<DepositOptions>(configuration.GetSection("Deposit"));
        Configure<WithdrawOptions>(configuration.GetSection("Withdraw"));
        Configure<DepositAddressOptions>(configuration.GetSection("DepositAddress"));
        Configure<ETransferServer.Grains.Options.NetworkOptions>(configuration.GetSection("CoinNetworks"));
        Configure<WithdrawNetworkOptions>(configuration.GetSection("WithdrawNetwork"));
        Configure<NetWorkReflectionOptions>(configuration.GetSection("NetWorkReflection"));
        Configure<NotifyTemplateOptions>(configuration.GetSection("NotifyTemplates"));
        Configure<ExchangeOptions>(configuration.GetSection("Exchange"));
        Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
        Configure<SwapInfosOptions>(configuration.GetSection("SwapInfos"));
        Configure<GraphQLOptions>(configuration.GetSection("GraphQL"));
        Configure<BlockChainInfoOptions>(configuration.GetSection("BlockChainInfo"));

        context.Services.AddHostedService<ETransferServerHostedService>();
        context.Services.AddOrleansExceptionHandler();
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
        context.Services.AddTransient<IExchangeProvider, GateIoProvider>();
        context.Services.AddTransient<IExchangeProvider, UniswapV3Provider>();

        ConfigureGraphQl(context, configuration);
        
        context.Services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                var rabbitMqConfig = configuration.GetSection("MassTransit:RabbitMQ").Get<Grains.Options.MassRabbitMqOptions>();
                cfg.Host(rabbitMqConfig.Host, rabbitMqConfig.Port, "/", h =>
                {
                    h.Username(rabbitMqConfig.UserName);
                    h.Password(rabbitMqConfig.Password);
                });
            });
        });
    } 

    
    
    private void ConfigureGraphQl(ServiceConfigurationContext context,
        IConfiguration configuration)
    {
        context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
            new NewtonsoftJsonSerializer()));
        context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
    }

    
}