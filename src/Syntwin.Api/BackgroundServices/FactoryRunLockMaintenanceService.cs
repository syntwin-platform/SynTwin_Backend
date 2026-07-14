using Microsoft.Extensions.Options;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.Robots.Options;

namespace Syntwin.Api.BackgroundServices;

public sealed class FactoryRunLockMaintenanceService : BackgroundService
{
    private const string WorkerLockKey = "locks:workers:factory-run-lock-maintenance";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<FactoryRunLockMaintenanceService> _logger;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _leaseTtl;
    private readonly TimeSpan _workerLockTtl;

    public FactoryRunLockMaintenanceService(
        IServiceScopeFactory scopeFactory,
        IDistributedLock distributedLock,
        IOptions<RobotRuntimeOptions> options,
        ILogger<FactoryRunLockMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedLock = distributedLock;
        _logger = logger;

        var intervalSeconds = Math.Max(
            5,
            options.Value.FactoryRunLockMaintenanceIntervalSeconds);
        var leaseTtlSeconds = Math.Max(
            intervalSeconds * 3,
            options.Value.FactoryRunBusyLockTtlSeconds);

        _interval = TimeSpan.FromSeconds(intervalSeconds);
        _leaseTtl = TimeSpan.FromSeconds(leaseTtlSeconds);
        _workerLockTtl = TimeSpan.FromSeconds(intervalSeconds * 2);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var workerLock = await _distributedLock.TryAcquireAsync(
                    WorkerLockKey,
                    _workerLockTtl,
                    stoppingToken);

                if (workerLock is not null)
                {
                    await MaintainLocksAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to maintain FactoryRun busy locks.");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task MaintainLocksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFactoryRunRepository>();
        var busyLock = scope.ServiceProvider.GetRequiredService<IRobotBusyLock>();
        var references = await repository.ListLockReferencesAsync(
            take: 1000,
            cancellationToken);

        foreach (var reference in references)
        {
            if (reference.ShouldRenew)
            {
                await busyLock.RenewAsync(
                    reference.RobotId,
                    reference.OwnerId,
                    _leaseTtl,
                    cancellationToken);
            }
            else
            {
                await busyLock.ReleaseAsync(
                    reference.RobotId,
                    reference.OwnerId,
                    cancellationToken);
            }
        }
    }
}
