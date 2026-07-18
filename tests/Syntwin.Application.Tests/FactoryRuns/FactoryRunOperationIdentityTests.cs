using Syntwin.Application.FactoryRuns.Models;

namespace Syntwin.Application.Tests.FactoryRuns;

public sealed class FactoryRunOperationIdentityTests
{
    [Fact]
    public void CreateCommandId_IsStableForTheSameOperation()
    {
        var factoryRunId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var targetId = Guid.Parse("22222222-2222-4222-8222-222222222222");

        var first = FactoryRunOperationIdentity.CreateCommandId(
            factoryRunId,
            targetId,
            "prepare");
        var retry = FactoryRunOperationIdentity.CreateCommandId(
            factoryRunId,
            targetId,
            "PREPARE");

        Assert.Equal(first, retry);
    }

    [Fact]
    public void CreateCommandId_SeparatesTargetsAndOperations()
    {
        var factoryRunId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var firstTargetId = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var secondTargetId = Guid.Parse("33333333-3333-4333-8333-333333333333");

        var prepare = FactoryRunOperationIdentity.CreateCommandId(
            factoryRunId,
            firstTargetId,
            "prepare");
        var run = FactoryRunOperationIdentity.CreateCommandId(
            factoryRunId,
            firstTargetId,
            "run");
        var otherTarget = FactoryRunOperationIdentity.CreateCommandId(
            factoryRunId,
            secondTargetId,
            "prepare");

        Assert.NotEqual(prepare, run);
        Assert.NotEqual(prepare, otherTarget);
    }
}
