using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ETransferServer.Silo.Extensions;

namespace ETransferServer.Silo;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting ETransferServer.Silo");
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddJsonFile("network.json");
            builder.Configuration.AddJsonFile("notify.json");
            builder.Host.AddAppSettingsSecretsJson()
                .UseOrleansSnapshot()
                .UseAutofac()
                .UseSerilog();

            await builder.AddApplicationAsync<ETransferServerOrleansSiloModule>();
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

    internal static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostcontext, services) =>
            {
                services.AddApplication<ETransferServerOrleansSiloModule>();
            })
            .ConfigureAppConfiguration((h,c)=>c.AddJsonFile("network.json")) 
            .UseOrleansSnapshot()
            #if !DEBUG
            .ConfigureAppConfiguration((h, c) => c.AddJsonFile("apollosettings.json"))
            .UseApollo()
            #endif
            .UseAutofac()
            .UseSerilog();
}