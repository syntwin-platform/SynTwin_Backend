using Microsoft.EntityFrameworkCore;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.FactoryRuns.Models;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class FactoryRunRepository : IFactoryRunRepository
{
    private readonly SyntwinDbContext _dbContext;

    public FactoryRunRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FactoryRun?> GetByIdAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.FactoryRuns
            .Include(factoryRun => factoryRun.Programs)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.FactoryRunProgram)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.PrepareCommand)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.Command)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                factoryRun => factoryRun.Id == factoryRunId,
                cancellationToken);
    }

    public Task<FactoryRun?> GetByIdForStartAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.FactoryRuns
            .Include(factoryRun => factoryRun.Programs)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.PrepareCommand)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.Command)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.Program)
                    .ThenInclude(program => program!.Steps)
            .Include(factoryRun => factoryRun.Targets)
                .ThenInclude(target => target.FactoryRunProgram)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                factoryRun => factoryRun.Id == factoryRunId,
                cancellationToken);
    }

    public Task<FactoryRun?> GetByIdForArmAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.FactoryRuns
            .Include(factoryRun => factoryRun.Programs)
            .Include(factoryRun => factoryRun.Targets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(
                factoryRun => factoryRun.Id == factoryRunId,
                cancellationToken);
    }

    public Task<FactoryRunTarget?> GetTargetByPrepareCommandIdAsync(
        Guid prepareCommandId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.FactoryRunTargets.FirstOrDefaultAsync(
            target => target.PrepareCommandId == prepareCommandId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<FactoryRunLockReference>> ListLockReferencesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var targets = await _dbContext.FactoryRunTargets
            .AsNoTracking()
            .Where(target => target.Status != FactoryRunTargetStatus.Pending)
            .OrderByDescending(target => target.UpdatedAtUtc ?? target.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .Select(target => new
            {
                target.Id,
                target.RobotId,
                target.CommandId,
                target.Status,
                PrepareCommandStatus = target.PrepareCommand == null
                    ? (CommandStatus?)null
                    : target.PrepareCommand.Status,
                RunCommandStatus = target.Command == null
                    ? (CommandStatus?)null
                    : target.Command.Status
            })
            .ToListAsync(cancellationToken);

        var references = new List<FactoryRunLockReference>(targets.Count * 2);

        foreach (var target in targets)
        {
            var isTerminal = target.Status is
                FactoryRunTargetStatus.Completed or
                FactoryRunTargetStatus.Failed or
                FactoryRunTargetStatus.Cancelled;
            var prepareCommandFailed = target.PrepareCommandStatus is
                CommandStatus.Failed or
                CommandStatus.Timeout or
                CommandStatus.Cancelled;
            var runCommandTerminal = target.RunCommandStatus is
                CommandStatus.Completed or
                CommandStatus.Failed or
                CommandStatus.Timeout or
                CommandStatus.Cancelled;

            if (isTerminal || prepareCommandFailed)
            {
                references.Add(new FactoryRunLockReference(
                    target.RobotId,
                    target.Id,
                    ShouldRenew: false));

                if (isTerminal && target.CommandId.HasValue)
                {
                    references.Add(new FactoryRunLockReference(
                        target.RobotId,
                        target.CommandId.Value,
                        ShouldRenew: false));
                }

                continue;
            }

            if (runCommandTerminal && target.CommandId.HasValue)
            {
                references.Add(new FactoryRunLockReference(
                    target.RobotId,
                    target.CommandId.Value,
                    ShouldRenew: false));
                continue;
            }

            var ownerId = target.Status is
                FactoryRunTargetStatus.Starting or
                FactoryRunTargetStatus.Armed or
                FactoryRunTargetStatus.Running
                    ? target.CommandId
                    : null;

            if (ownerId.HasValue)
            {
                references.Add(new FactoryRunLockReference(
                    target.RobotId,
                    ownerId.Value,
                    ShouldRenew: true));
            }
        }

        return references;
    }

    public async Task AddAsync(
        FactoryRun factoryRun,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.FactoryRuns.AddAsync(factoryRun, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
