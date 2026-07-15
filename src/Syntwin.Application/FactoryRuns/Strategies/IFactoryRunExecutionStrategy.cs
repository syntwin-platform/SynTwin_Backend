using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Strategies;

public interface IFactoryRunExecutionStrategy
{
    FactoryCoordinationMode Mode { get; }

    bool UsesSynchronizedBarrier { get; }

    IReadOnlyList<FactoryRunTarget> SelectStartTargets(FactoryRun factoryRun);
}
