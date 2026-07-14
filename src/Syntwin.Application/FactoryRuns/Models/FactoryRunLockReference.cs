namespace Syntwin.Application.FactoryRuns.Models;

public sealed record FactoryRunLockReference(
    Guid RobotId,
    Guid OwnerId,
    bool ShouldRenew);
