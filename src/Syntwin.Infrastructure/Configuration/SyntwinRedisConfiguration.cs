using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Syntwin.Infrastructure.Configuration;

public static class SyntwinRedisConfiguration
{
    public static ConfigurationOptions CreateSyntwinRedisOptions(
        this IConfiguration configuration)
    {
        var connectionString =
            configuration["Redis:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Redis:ConnectionString is required.");
        }

        var options = ConfigurationOptions.Parse(connectionString);

        options.AbortOnConnectFail = false;
        options.ConnectRetry = 5;
        options.ConnectTimeout = 10_000;
        options.SyncTimeout = 10_000;
        options.KeepAlive = 30;
        options.ReconnectRetryPolicy = new ExponentialRetry(5_000);

        return options;
    }
}