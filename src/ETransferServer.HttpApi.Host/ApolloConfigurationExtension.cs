using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ETransferServer;

public static class ApolloConfigurationExtension
{
    public static IHostBuilder UseApollo(this IHostBuilder builder)
    {
        return builder
            .ConfigureAppConfiguration(config =>
            {
                Log.Information("Apollo json: {apollo}", config.Build().GetSection("apollo"));
                config.AddApollo(config.Build().GetSection("apollo"));
            });
    }
}