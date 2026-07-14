using Syntwin.Application.FactoryRuns.Dtos;

namespace Syntwin.Application.FactoryRuns.Interfaces;

public interface IFactoryRunProgramPreparationExecutor
{
    Task<IReadOnlyList<FactoryRunPreparedProgram>> CreateAndPublishManyAsync(
        Guid userId,
        IReadOnlyCollection<FactoryRunProgramPreparationItem> items,
        string? ipAddress,
        int maxConcurrency,
        CancellationToken cancellationToken = default);
}