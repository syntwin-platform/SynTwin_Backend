using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Infrastructure.Configuration;

namespace Syntwin.Hosting.Realtime;

public static class RealtimeServiceCollectionExtensions
{
    public static IServiceCollection AddSyntwinRealtime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddSignalR()
            .AddStackExchangeRedis(options =>
            {
                var redisOptions =
                    configuration.CreateSyntwinRedisOptions();
                redisOptions.ChannelPrefix =
                    RedisChannel.Literal("syntwin:signalr");

                options.Configuration = redisOptions;
            });

        services.AddScoped<
            IRobotRealtimeNotifier,
            SignalRRobotRealtimeNotifier>();

        return services;
    }
}
