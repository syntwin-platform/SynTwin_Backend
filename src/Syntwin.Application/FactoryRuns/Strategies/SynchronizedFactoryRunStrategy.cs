using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Strategies;

public sealed class SynchronizedFactoryRunStrategy : IFactoryRunExecutionStrategy
{
    public FactoryCoordinationMode Mode => FactoryCoordinationMode.Synchronized;

    public bool UsesSynchronizedBarrier => true;

    public IReadOnlyList<FactoryRunTarget> SelectStartTargets(FactoryRun factoryRun)
    {
        ArgumentNullException.ThrowIfNull(factoryRun);

        var targets = factoryRun.FailurePolicy == FactoryFailurePolicy.IsolateTarget
            ? factoryRun.Targets.Where(target => target.Status == FactoryRunTargetStatus.Ready)
            : factoryRun.Targets;

        return targets
            .OrderBy(target => target.CreatedAtUtc)
            .ToList();
    }
}
