using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ETransferServer
{
    public class Program
    {
        public async static Task<int> Main(string[] args)
        {
            System.Threading.ThreadPool.SetMinThreads(300, 300);
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            try
            {
                Log.Information("Starting ETransferServer.HttpApi.Host");

                var builder = WebApplication.CreateBuilder(args);
                builder.Configuration.AddJsonFile("apollo.appsettings.json");
                builder.Configuration.AddJsonFile("token.json");
                builder.Configuration.AddJsonFile("network.json");
                builder.Configuration.AddJsonFile("charge.json");
                builder.Host.AddAppSettingsSecretsJson()
                    .UseAutofac()
                #if !DEBUG
                    .UseApollo()
                #endif
                    .UseOrleansClient()
                    .UseSerilog();

                await builder.AddApplicationAsync<ETransferServerHttpApiHostModule>();
                var app = builder.Build();
                await app.InitializeApplicationAsync();
                await app.RunAsync();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly!");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}