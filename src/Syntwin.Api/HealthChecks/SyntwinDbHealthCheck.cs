using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Syntwin.Infrastructure.Persistence;

namespace Syntwin.Api.HealthChecks;

public sealed class SyntwinDbHealthCheck : IHealthCheck
{
    private readonly SyntwinDbContext _dbContext;

    public SyntwinDbHealthCheck(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("SQL Server is reachable.")
            : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
    }
}