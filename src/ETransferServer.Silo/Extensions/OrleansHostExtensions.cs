using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Statistics;
using Orleans.Streams.Kafka.Config;
using ETransferServer.Common;

namespace ETransferServer.Silo.Extensions;

public static class OrleansHostExtensions
{
    public static ISiloBuilder UseKafkaMqStreamProvider(this ISiloBuilder siloBuilder,
        IConfigurationRoot configuration)
    {
        siloBuilder
            .AddKafka(CommonConstant.StreamConstant.MessageStreamNameSpace)
            .WithOptions(options =>
            {
                var topics = configuration.GetSection("KafkaStream:Topics").Get<List<string>>() ??
                             new List<string> { "DefaultTopic" };
                options.BrokerList = configuration.GetSection("KafkaStream:BrokerList").Get<List<string>>();
                options.ConsumerGroupId = CommonConstant.StreamConstant.MessageStreamNameSpace;
                options.ConsumeMode = ConsumeMode.LastCommittedMessage;
                foreach (var topic in topics)
                {
                    options.AddTopic(topic, new TopicCreationConfig { AutoCreate = true});
                }
                options.MessageMaxBytes = configuration.GetSection("KafkaStream:MessageMaxBytes").Get<int>();
            })
            .AddJson()
            .AddLoggingTracker().Build();
        return siloBuilder;
    }


    public static IHostBuilder UseOrleansSnapshot(this IHostBuilder hostBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        var configSection = configuration.GetSection("Orleans");
        if (configSection == null)
            throw new ArgumentNullException(nameof(configSection), "The OrleansServer node is missing");
        return hostBuilder.UseOrleans(siloBuilder =>
        {
            //Configure OrleansSnapshot
            siloBuilder
                .ConfigureEndpoints(advertisedIP: IPAddress.Parse(configSection.GetValue<string>("AdvertisedIP")),
                    siloPort: configSection.GetValue<int>("SiloPort"),
                    gatewayPort: configSection.GetValue<int>("GatewayPort"), listenOnAnyHostAddress: true)
                .UseMongoDBClient(configSection.GetValue<string>("MongoDBClient"))
                .UseMongoDBClustering(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    ;
                    options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                })
                .AddMongoDBGrainStorage("Default", (MongoDBGrainStorageOptions op) =>
                {
                    op.CollectionPrefix = "GrainStorage";
                    op.DatabaseName = configSection.GetValue<string>("DataBase");

                    op.ConfigureJsonSerializerSettings = jsonSettings =>
                    {
                        // jsonSettings.ContractResolver = new PrivateSetterContractResolver();
                        jsonSettings.NullValueHandling = NullValueHandling.Include;
                        jsonSettings.DefaultValueHandling = DefaultValueHandling.Populate;
                        jsonSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                    };
                })
                .UseMongoDBReminders(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    options.CreateShardKeyForCosmos = false;
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = configSection.GetValue<string>("ClusterId");
                    options.ServiceId = configSection.GetValue<string>("ServiceId");
                })
                .AddMemoryGrainStorage("PubSubStore")
                .ConfigureApplicationParts(parts => parts.AddFromApplicationBaseDirectory())
                .UseDashboard(options =>
                {
                    options.Username = configSection.GetValue<string>("DashboardUserName");
                    options.Password = configSection.GetValue<string>("DashboardPassword");
                    options.Host = "*";
                    options.Port = configSection.GetValue<int>("DashboardPort");
                    options.HostSelf = true;
                    options.CounterUpdateIntervalMs = configSection.GetValue<int>("DashboardCounterUpdateIntervalMs");
                })
                .UseLinuxEnvironmentStatistics()
                .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); })
                .AddStartupTask<GrainStartupTask>()
                .UseKafkaMqStreamProvider(configuration);
            //.AddStartupTask<TestWithdraw>();
        });
    }
}