using Syntwin.Domain.Entities;

using Syntwin.Application.FactoryRuns.Models;

namespace Syntwin.Application.FactoryRuns.Interfaces;

public interface IFactoryRunRepository
{
    Task<FactoryRun?> GetByIdAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default);

    Task<FactoryRun?> GetByIdForStartAsync(
    Guid factoryRunId,
    CancellationToken cancellationToken = default);

    Task<FactoryRun?> GetByIdForArmAsync(
    Guid factoryRunId,
    CancellationToken cancellationToken = default);

    Task<FactoryRunTarget?> GetTargetByPrepareCommandIdAsync(
        Guid prepareCommandId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FactoryRunLockReference>> ListLockReferencesAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        FactoryRun factoryRun,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
