using System;
using System.IO;
using System.Linq;
using AElf.OpenTelemetry;
using AutoResponseWrapper;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using ETransferServer.MongoDB;
using ETransferServer.Options;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.AutoMapper;
using Volo.Abp.BlobStoring.Aliyun;
using Volo.Abp.Caching;
using Volo.Abp.Caching.StackExchangeRedis;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.Tokens;
using Volo.Abp.Swashbuckle;
using Volo.Abp.VirtualFileSystem;
using TokenOptions = ETransferServer.Options.TokenOptions;

namespace ETransferServer
{
    [DependsOn(
        typeof(ETransferServerHttpApiModule),
        typeof(AbpAutofacModule),
        typeof(AbpCachingStackExchangeRedisModule),
        typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
        typeof(ETransferServerApplicationModule),
        typeof(ETransferServerMongoDbModule),
        typeof(AbpAspNetCoreSerilogModule),
        typeof(AbpSwashbuckleModule),
        typeof(AbpEventBusRabbitMqModule),
        typeof(AbpBlobStoringAliyunModule),
        typeof(AbpIdentityDomainModule),
        typeof(OpenTelemetryModule)
    )]
    public class ETransferServerHttpApiHostModule : AbpModule
    {
        public override void PreConfigureServices(ServiceConfigurationContext context)
        {
            PreConfigure<IdentityBuilder>(builder =>
            {
                builder.AddDefaultTokenProviders();
            });
        }
        
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            Configure<AbpAutoMapperOptions>(options => { options.AddMaps<ETransferServerHttpApiHostModule>(); });
            var configuration = context.Services.GetConfiguration();
            var hostingEnvironment = context.Services.GetHostingEnvironment();
            Configure<SignatureServiceOption>(configuration.GetSection("SignatureService"));
            Configure<ChainOptions>(configuration.GetSection("Chains"));
            Configure<TokenOptions>(configuration.GetSection("TokenOptions"));
            Configure<NetworkOptions>(configuration.GetSection("NetworkOptions"));
            Configure<TokenInfoOptions>(configuration.GetSection("TokenInfo"));
            Configure<DepositInfoOptions>(configuration.GetSection("DepositInfo"));
            Configure<WithdrawInfoOptions>(configuration.GetSection("WithdrawInfo"));
            Configure<CoinGeckoOptions>(configuration.GetSection("CoinGecko"));
            Configure<CoBoOptions>(configuration.GetSection("CoBo"));
            Configure<HubOptions>(configuration.GetSection("Hub"));
            Configure<SwapInfosOptions>(configuration.GetSection("SwapInfos"));
            Configure<StringEncryptionOptions>(configuration.GetSection("StringEncryption"));

            ConfigureConventionalControllers();
            // ConfigureAuthentication(context, configuration);
            ConfigureLocalization();
            ConfigureCache(configuration);
            ConfigureVirtualFileSystem(context);
            ConfigureRedis(context, configuration, hostingEnvironment);
            ConfigureCors(context, configuration);
            ConfigureSwaggerServices(context, configuration);
            ConfigureTokenCleanupService();
            ConfigureHub(context, configuration);
            ConfigureGraphQl(context, configuration);
            context.Services.AddAutoResponseWrapper();
            ConfigureAuthentication(context, configuration);
        }

        private void ConfigureCache(IConfiguration configuration)
        {
            Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "ETransferServer:"; });
        }
        
        private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = configuration["AuthServer:Authority"];
                    options.RequireHttpsMetadata = Convert.ToBoolean(configuration["AuthServer:RequireHttpsMetadata"]);
                    options.Audience = "ETransferServer";
                });
        }

        private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
        {
            var hostingEnvironment = context.Services.GetHostingEnvironment();

            if (hostingEnvironment.IsDevelopment())
            {
                Configure<AbpVirtualFileSystemOptions>(options =>
                {
                    options.FileSets.ReplaceEmbeddedByPhysical<ETransferServerDomainSharedModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}ETransferServer.Domain.Shared"));
                    options.FileSets.ReplaceEmbeddedByPhysical<ETransferServerDomainModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}ETransferServer.Domain"));
                    options.FileSets.ReplaceEmbeddedByPhysical<ETransferServerApplicationContractsModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}ETransferServer.Application.Contracts"));
                    options.FileSets.ReplaceEmbeddedByPhysical<ETransferServerApplicationModule>(
                        Path.Combine(hostingEnvironment.ContentRootPath,
                            $"..{Path.DirectorySeparatorChar}ETransferServer.Application"));
                });
            }
        }

        private void ConfigureConventionalControllers()
        {
            Configure<AbpAspNetCoreMvcOptions>(options =>
            {
                options.ConventionalControllers.Create(typeof(ETransferServerHttpApiModule).Assembly);
            });
        }


        private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddAbpSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "ETransferServer API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    Name = "Authorization",
                    Scheme = "bearer",
                    Description = "Specify the authorization token.",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new string[] { }
                    }
                });
            });
        }

        private void ConfigureLocalization()
        {
            Configure<AbpLocalizationOptions>(options =>
            {
                options.Languages.Add(new LanguageInfo("ar", "ar", "العربية"));
                options.Languages.Add(new LanguageInfo("cs", "cs", "Čeština"));
                options.Languages.Add(new LanguageInfo("en", "en", "English"));
                options.Languages.Add(new LanguageInfo("en-GB", "en-GB", "English (UK)"));
                options.Languages.Add(new LanguageInfo("fi", "fi", "Finnish"));
                options.Languages.Add(new LanguageInfo("fr", "fr", "Français"));
                options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi"));
                options.Languages.Add(new LanguageInfo("it", "it", "Italian"));
                options.Languages.Add(new LanguageInfo("hu", "hu", "Magyar"));
                options.Languages.Add(new LanguageInfo("pt-BR", "pt-BR", "Português"));
                options.Languages.Add(new LanguageInfo("ru", "ru", "Русский"));
                options.Languages.Add(new LanguageInfo("sk", "sk", "Slovak"));
                options.Languages.Add(new LanguageInfo("tr", "tr", "Türkçe"));
                options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
                options.Languages.Add(new LanguageInfo("zh-Hant", "zh-Hant", "繁體中文"));
                options.Languages.Add(new LanguageInfo("de-DE", "de-DE", "Deutsch"));
                options.Languages.Add(new LanguageInfo("es", "es", "Español"));
            });
        }

        private void ConfigureRedis(
            ServiceConfigurationContext context,
            IConfiguration configuration,
            IWebHostEnvironment hostingEnvironment)
        {
            if (!hostingEnvironment.IsDevelopment())
            {
                var redis = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
                context.Services
                    .AddDataProtection()
                    .PersistKeysToStackExchangeRedis(redis, "ETransferServer-Protection-Keys");
            }
        }

        private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
        {
            context.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder
                        .WithOrigins(
                            configuration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .WithAbpExposedHeaders()
                        .SetIsOriginAllowedToAllowWildcardSubdomains()
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        private void ConfigureTokenCleanupService()
        {
            Configure<TokenCleanupOptions>(x => x.IsCleanupEnabled = false);
        }
        
        private void ConfigureHub(ServiceConfigurationContext context,
            IConfiguration configuration)
        {
            var multiplexer = ConnectionMultiplexer.Connect(configuration["Redis:Configuration"]);
            context.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        }

        private void ConfigureGraphQl(ServiceConfigurationContext context,
            IConfiguration configuration)
        {
            context.Services.AddSingleton(new GraphQLHttpClient(configuration["GraphQL:Configuration"],
                new NewtonsoftJsonSerializer()));
            context.Services.AddScoped<IGraphQLClient>(sp => sp.GetRequiredService<GraphQLHttpClient>());
        }

        public override void OnApplicationInitialization(ApplicationInitializationContext context)
        {
            var app = context.GetApplicationBuilder();
            var env = context.GetEnvironment();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseCorrelationId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();

            app.UseAbpRequestLocalization();
            app.UseAuthorization();

            // if (env.IsDevelopment())
            // {
            app.UseSwagger();
            app.UseAbpSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support APP API"); });
            // }

            app.UseAuditing();
            app.UseAbpSerilogEnrichers();
            app.UseUnitOfWork();
            app.UseConfiguredEndpoints();
        }

        public override void OnApplicationShutdown(ApplicationShutdownContext context)
        {
            
        }
    }

}