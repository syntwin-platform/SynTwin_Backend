using Syntwin.Application.FactoryRuns.Models;

namespace Syntwin.Application.FactoryRuns.Interfaces;

public interface IFactoryRunArmBarrier
{
    Task<FactoryRunArmBarrierRegistration> RegisterAsync(
        Guid factoryRunId,
        Guid targetId,
        int expectedParticipantCount,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default);

    Task<FactoryRunArmBarrierReadyState?> GetReadyAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default);

    Task PublishReadyAsync(
        Guid factoryRunId,
        Guid sealOwnerTargetId,
        FactoryRunArmBarrierReadyState ready,
        CancellationToken cancellationToken = default);

    Task RestoreReadyAsync(
        Guid factoryRunId,
        FactoryRunArmBarrierReadyState ready,
        CancellationToken cancellationToken = default);

    Task ReleaseSealAsync(
        Guid factoryRunId,
        Guid sealOwnerTargetId,
        CancellationToken cancellationToken = default);
}
