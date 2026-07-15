using Syntwin.Application.FactoryRuns.Strategies;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Tests.FactoryRuns;

public sealed class FactoryRunExecutionStrategyTests
{
    [Fact]
    public void ParallelIndependent_SelectsOnlyReadyTargetsWithoutBarrier()
    {
        var readyTarget = CreateTarget(FactoryRunTargetStatus.Ready, 1);
        var failedTarget = CreateTarget(FactoryRunTargetStatus.Failed, 2);
        var strategy = new ParallelIndependentFactoryRunStrategy();
        var factoryRun = new FactoryRun
        {
            CoordinationMode = FactoryCoordinationMode.ParallelIndependent,
            FailurePolicy = FactoryFailurePolicy.IsolateTarget,
            Targets = [failedTarget, readyTarget]
        };

        var selectedTargets = strategy.SelectStartTargets(factoryRun);

        Assert.False(strategy.UsesSynchronizedBarrier);
        Assert.Collection(
            selectedTargets,
            target => Assert.Same(readyTarget, target));
    }

    [Fact]
    public void Synchronized_IsolateTarget_SelectsSurvivingReadyTargetsWithBarrier()
    {
        var readyTarget = CreateTarget(FactoryRunTargetStatus.Ready, 1);
        var failedTarget = CreateTarget(FactoryRunTargetStatus.Failed, 2);
        var strategy = new SynchronizedFactoryRunStrategy();
        var factoryRun = new FactoryRun
        {
            CoordinationMode = FactoryCoordinationMode.Synchronized,
            FailurePolicy = FactoryFailurePolicy.IsolateTarget,
            Targets = [readyTarget, failedTarget]
        };

        var selectedTargets = strategy.SelectStartTargets(factoryRun);

        Assert.True(strategy.UsesSynchronizedBarrier);
        Assert.Collection(
            selectedTargets,
            target => Assert.Same(readyTarget, target));
    }

    [Fact]
    public void Resolver_ReturnsStrategyForRequestedMode()
    {
        var resolver = new FactoryRunExecutionStrategyResolver(
        [
            new ParallelIndependentFactoryRunStrategy(),
            new SynchronizedFactoryRunStrategy()
        ]);

        var strategy = resolver.Resolve(FactoryCoordinationMode.ParallelIndependent);

        Assert.IsType<ParallelIndependentFactoryRunStrategy>(strategy);
    }

    private static FactoryRunTarget CreateTarget(
        FactoryRunTargetStatus status,
        int creationOrder)
    {
        return new FactoryRunTarget
        {
            Id = Guid.NewGuid(),
            RobotId = Guid.NewGuid(),
            Status = status,
            CreatedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(creationOrder),
            UpdatedAtUtc = DateTimeOffset.UnixEpoch.AddSeconds(creationOrder)
        };
    }
}
