using System;
using AutoMapper;
using ETransferServer.Common;
using ETransferServer.Grains;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Hosting;
using Orleans.TestingHost;
using Microsoft.Extensions.Configuration;
using Volo.Abp.AutoMapper;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ObjectMapping;
using Volo.Abp.Reflection;

namespace ETransferServer;

public class ClusterFixture: IDisposable, ISingletonDependency
{
    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
        builder.AddClientBuilderConfigurator<TestClientBuilderConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    public TestCluster Cluster { get; private set; }
    
    private class TestSiloConfigurations : ISiloConfigurator 
    {
        public void Configure(ISiloBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices(services =>
                {
                    services.AddMemoryCache();
                    services.AddDistributedMemoryCache();
                    services.AddAutoMapper(typeof(ETransferServerGrainsModule).Assembly);

                    services.AddSingleton(typeof(IDistributedCache), typeof(MemoryDistributedCache));
                    // services.AddSingleton(typeof(IDistributedCache<>), typeof(MemoryDistributedCache<>));
                    services.AddSingleton(typeof(IDistributedCache<,>), typeof(DistributedCache<,>));

                    services.Configure<AbpDistributedCacheOptions>(cacheOptions =>
                    {
                        cacheOptions.GlobalCacheEntryOptions.SlidingExpiration = TimeSpan.FromMinutes(20);
                    });
                    services.OnExposing(onServiceExposingContext =>
                    {
                        //Register types for IObjectMapper<TSource, TDestination> if implements
                        var implementedTypes = ReflectionHelper.GetImplementedGenericTypes(
                            onServiceExposingContext.ImplementationType,
                            typeof(IObjectMapper<,>)
                        );

                        foreach (var type in implementedTypes)
                        {
                            onServiceExposingContext.ExposedTypes.Add(new ServiceIdentifier(type));
                        }
                    });
                    services.AddTransient(
                        typeof(IObjectMapper<>),
                        typeof(DefaultObjectMapper<>)
                    );
                    services.AddTransient(
                        typeof(IObjectMapper),
                        typeof(DefaultObjectMapper)
                    );
                    services.AddTransient(typeof(IAutoObjectMappingProvider),
                        typeof(AutoMapperAutoObjectMappingProvider));
                    services.AddTransient(sp => new MapperAccessor()
                    {
                        Mapper = sp.GetRequiredService<IMapper>()
                    });
                    services.AddTransient<IMapperAccessor>(provider => provider.GetRequiredService<MapperAccessor>());
                })
                .AddMemoryStreams(CommonConstant.StreamConstant.MessageStreamName)
                .AddMemoryGrainStorage("PubSubStore")
                .AddMemoryGrainStorageAsDefault();
        }
    }
    
    private class TestClientBuilderConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => clientBuilder
            .AddMemoryStreams(CommonConstant.StreamConstant.MessageStreamName);
    }
}

public class MapperAccessor : IMapperAccessor
{
    public IMapper Mapper { get; set; }
}