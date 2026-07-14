using InfluxDB.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Syntwin.Infrastructure.Telemetry;

namespace Syntwin.Api.HealthChecks;

public sealed class InfluxDbHealthCheck : IHealthCheck
{
    private readonly InfluxDbOptions _options;

    public InfluxDbHealthCheck(IOptions<InfluxDbOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("InfluxDB is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.Url) ||
            string.IsNullOrWhiteSpace(_options.Token) ||
            string.IsNullOrWhiteSpace(_options.Org) ||
            string.IsNullOrWhiteSpace(_options.Bucket))
        {
            return HealthCheckResult.Unhealthy("InfluxDB configuration is incomplete.");
        }

        try
        {
            using var client = new InfluxDBClient(
                _options.Url,
                _options.Token);

            var isReachable = await client.PingAsync();

            return isReachable
                ? HealthCheckResult.Healthy("InfluxDB is reachable.")
                : HealthCheckResult.Unhealthy("InfluxDB ping failed.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy(
                "InfluxDB is not reachable.",
                exception);
        }
    }
}
