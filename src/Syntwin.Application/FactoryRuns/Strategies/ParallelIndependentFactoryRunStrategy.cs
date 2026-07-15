using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Strategies;

public sealed class ParallelIndependentFactoryRunStrategy : IFactoryRunExecutionStrategy
{
    public FactoryCoordinationMode Mode => FactoryCoordinationMode.ParallelIndependent;

    public bool UsesSynchronizedBarrier => false;

    public IReadOnlyList<FactoryRunTarget> SelectStartTargets(FactoryRun factoryRun)
    {
        ArgumentNullException.ThrowIfNull(factoryRun);

        return factoryRun.Targets
            .Where(target => target.Status == FactoryRunTargetStatus.Ready)
            .OrderBy(target => target.CreatedAtUtc)
            .ToList();
    }
}
