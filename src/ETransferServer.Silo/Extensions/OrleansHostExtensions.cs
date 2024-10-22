using System.Net;
using ETransfer.Silo.MongoDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;
using Orleans.Streams.Kafka.Config;
using ETransferServer.Common;
using ETransferServer.Silo.MongoDB;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Providers.MongoDB.StorageProviders.Serializers;
using Orleans.Providers.Streams.Generator;
using Orleans.Serialization;

namespace ETransferServer.Silo.Extensions;

public static class OrleansHostExtensions
{
    public static IHostBuilder UseOrleans(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseOrleans((context, siloBuilder) =>
        {
            var configuration = context.Configuration;
            var configSection = context.Configuration.GetSection("Orleans");
            if (configSection == null)
                throw new ArgumentNullException(nameof(configSection), "The OrleansServer node is missing");
            var isRunningInKubernetes = configSection.GetValue<bool>("IsRunningInKubernetes");
            var advertisedIP = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("POD_IP")
                : configSection.GetValue<string>("AdvertisedIP");
            var clusterId = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("ORLEANS_CLUSTER_ID")
                : configSection.GetValue<string>("ClusterId");
            var serviceId = isRunningInKubernetes
                ? Environment.GetEnvironmentVariable("ORLEANS_SERVICE_ID")
                : configSection.GetValue<string>("ServiceId");
            siloBuilder
                .ConfigureEndpoints(advertisedIP: IPAddress.Parse(advertisedIP),
                    siloPort: configSection.GetValue<int>("SiloPort"),
                    gatewayPort: configSection.GetValue<int>("GatewayPort"), listenOnAnyHostAddress: true)
                .UseMongoDBClient(configSection.GetValue<string>("MongoDBClient"))
                .UseMongoDBClustering(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    options.Strategy = MongoDBMembershipStrategy.SingleDocument;
                })
                .Configure<JsonGrainStateSerializerOptions>(options => options.ConfigureJsonSerializerSettings =
                    settings =>
                    {
                        settings.NullValueHandling = NullValueHandling.Include;
                        settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
                        settings.DefaultValueHandling = DefaultValueHandling.Populate;
                    })
                .ConfigureServices(services => services.AddSingleton<IGrainStateSerializer, ETransferJsonGrainStateSerializer>())
                .AddETransferMongoDBGrainStorage("Default", (MongoDBGrainStorageOptions op) =>
                {
                    op.CollectionPrefix = "GrainStorage";
                    op.DatabaseName = configSection.GetValue<string>("DataBase");

                    var grainIdPrefix = configSection
                        .GetSection("GrainSpecificIdPrefix").GetChildren().ToDictionary(o => o.Key.ToLower(), o => o.Value);
                    op.KeyGenerator = id =>
                    {
                        var grainType = id.Type.ToString();
                        if (grainIdPrefix.TryGetValue(grainType, out var prefix))
                        {
                            return prefix.StartsWith("GrainReference=000000") ? $"{prefix}+{id.Key}" : prefix;
                        }

                        return id.ToString();
                    };
                    op.CreateShardKeyForCosmos = configSection.GetValue<bool>("CreateShardKeyForMongoDB", false);
                })
                .Configure<GrainCollectionOptions>(options =>
                {
                    // Override the value of CollectionAge to
                    var collection = configSection.GetSection(nameof(GrainCollectionOptions.ClassSpecificCollectionAge))
                        .GetChildren();
                    foreach (var item in collection)
                    {
                        options.ClassSpecificCollectionAge[item.Key] = TimeSpan.FromSeconds(int.Parse(item.Value));
                    }
                })
                .Configure<GrainCollectionNameOptions>(options =>
                {
                    var collectionName = configSection
                        .GetSection(nameof(GrainCollectionNameOptions.GrainSpecificCollectionName)).GetChildren();
                    options.GrainSpecificCollectionName = collectionName.ToDictionary(o => o.Key, o => o.Value);
                })
                .UseMongoDBReminders(options =>
                {
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                    options.CreateShardKeyForCosmos = false;
                })
                .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = clusterId;
                    options.ServiceId = serviceId;
                })
                .Configure<SiloMessagingOptions>(options =>
                {
                    options.ResponseTimeout = TimeSpan.FromSeconds(configSection.GetValue<int>("GrainResponseTimeOut"));
                    options.MaxMessageBodySize = configSection.GetValue<int>("GrainMaxMessageBodySize");
                    options.MaxForwardCount = configSection.GetValue<int>("MaxForwardCount");
                })
                .AddETransferMongoDBGrainStorage("PubSubStore", options =>
                {
                    // Config PubSubStore Storage for Persistent Stream 
                    options.CollectionPrefix = "StreamStorage";
                    options.DatabaseName = configSection.GetValue<string>("DataBase");
                })
                .Configure<ExceptionSerializationOptions>(options=>
                {
                    options.SupportedNamespacePrefixes.Add("Volo.Abp");
                    options.SupportedNamespacePrefixes.Add("Newtonsoft.Json");
                })
                .UseDashboard(options =>
                {
                    options.Username = configSection.GetValue<string>("DashboardUserName");
                    options.Password = configSection.GetValue<string>("DashboardPassword");
                    options.Host = "*";
                    options.Port = configSection.GetValue<int>("DashboardPort");
                    options.HostSelf = true;
                    options.CounterUpdateIntervalMs = configSection.GetValue<int>("DashboardCounterUpdateIntervalMs");
                })
                .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Debug).AddConsole(); })
                .AddStartupTask<GrainStartupTask>()
                .AddActivityPropagation()
                .AddPersistentStreams(CommonConstant.StreamConstant.MessageStreamNameSpace, GeneratorAdapterFactory.Create,
                    providerConfigurator => providerConfigurator
                        .Configure<HashRingStreamQueueMapperOptions>(ob => ob.Configure(
                            options=>{ options.TotalQueueCount = HashRingStreamQueueMapperOptions.DEFAULT_NUM_QUEUES; })))
                .AddKafka(CommonConstant.StreamConstant.MessageStreamNameSpace)
                .WithOptions(options =>
                {
                    options.BrokerList = configuration.GetSection("KafkaStream:BrokerList").Get<List<string>>();
                    options.ConsumerGroupId = CommonConstant.StreamConstant.MessageStreamNameSpace;
                    options.ConsumeMode = ConsumeMode.LastCommittedMessage;

                    var topics = configuration.GetSection("KafkaStream:Topics").Get<List<string>>() ??
                                 new List<string> { "DefaultTopic" };
                    foreach (var topic in topics)
                    {
                        options.AddTopic(topic, new TopicCreationConfig { AutoCreate = true});
                    }
                    options.MessageMaxBytes = configuration.GetSection("KafkaStream:MessageMaxBytes").Get<int>();
                })
                .AddJson()
                .AddLoggingTracker()
                .Build();
        });
    }
}