using Syntwin.Domain.Enums;

namespace Syntwin.Application.FactoryRuns.Strategies;

public sealed class FactoryRunExecutionStrategyResolver
{
    private readonly IReadOnlyDictionary<FactoryCoordinationMode, IFactoryRunExecutionStrategy>
        _strategies;

    public FactoryRunExecutionStrategyResolver(
        IEnumerable<IFactoryRunExecutionStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(strategy => strategy.Mode);
    }

    public IFactoryRunExecutionStrategy Resolve(FactoryCoordinationMode mode)
    {
        if (_strategies.TryGetValue(mode, out var strategy))
        {
            return strategy;
        }

        throw new InvalidOperationException(
            $"No FactoryRun execution strategy is registered for mode {mode}.");
    }
}
