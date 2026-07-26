using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Syntwin.Hosting.HealthChecks;

public static class HealthEndpointExtensions
{
    public static IServiceCollection AddSyntwinDependencyHealthChecks(
        this IServiceCollection services)
    {
        services
            .AddHealthChecks()
            .AddCheck<SyntwinDbHealthCheck>(
                "sqlserver",
                tags: ["ready"])
            .AddCheck<RedisHealthCheck>(
                "redis",
                tags: ["ready"])
            .AddCheck<InfluxDbHealthCheck>(
                "influxdb",
                tags: ["ready"]);

        return services;
    }

    public static IEndpointRouteBuilder MapSyntwinHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(
            "/health/live",
            new HealthCheckOptions
            {
                Predicate = _ => false
            });

        endpoints.MapHealthChecks(
            "/health/ready",
            new HealthCheckOptions
            {
                Predicate = registration =>
                    registration.Tags.Contains("ready"),
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";

                    var payload = new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(entry => new
                        {
                            name = entry.Key,
                            status = entry.Value.Status.ToString(),
                            description = entry.Value.Description
                        })
                    };

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(payload));
                }
            });

        return endpoints;
    }
}
