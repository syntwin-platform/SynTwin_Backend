using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.RobotPrograms.Interfaces;

namespace Syntwin.Infrastructure.FactoryRuns;

public sealed class ScopedFactoryRunProgramPreparationExecutor
    : IFactoryRunProgramPreparationExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopedFactoryRunProgramPreparationExecutor> _logger;

    public ScopedFactoryRunProgramPreparationExecutor(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedFactoryRunProgramPreparationExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FactoryRunPreparedProgram>> CreateAndPublishManyAsync(
        Guid userId,
        IReadOnlyCollection<FactoryRunProgramPreparationItem> items,
        string? ipAddress,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return Array.Empty<FactoryRunPreparedProgram>();
        }

        var boundedConcurrency = Math.Clamp(maxConcurrency, 1, 4);
        using var gate = new SemaphoreSlim(
            boundedConcurrency,
            boundedConcurrency);

        var tasks = items.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken);
            FactoryRunPreparedProgram? createdProgram = null;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();

                var programService =
                    scope.ServiceProvider.GetRequiredService<IRobotProgramService>();

                var importedProgram = await programService.CreateAsync(
                    userId,
                    item.RobotId,
                    item.CreateProgramRequest,
                    ipAddress,
                    cancellationToken);

                if (importedProgram is null)
                {
                    throw new InvalidOperationException(
                        $"Robot {item.RobotId} was not found or access was denied.");
                }

                createdProgram = new FactoryRunPreparedProgram
                {
                    RobotId = item.RobotId,
                    Program = importedProgram
                };

                var publishedProgram = await programService.PublishAsync(
                    userId,
                    item.RobotId,
                    importedProgram.Id,
                    ipAddress,
                    cancellationToken);

                if (publishedProgram is null)
                {
                    throw new InvalidOperationException(
                        $"Program for robot {item.RobotId} could not be published.");
                }

                return new FactoryRunPreparedProgram
                {
                    RobotId = item.RobotId,
                    Program = publishedProgram
                };
            }
            catch (OperationCanceledException)
            {
                if (createdProgram is not null)
                {
                    await ArchivePreparedProgramBestEffortAsync(
                        userId,
                        createdProgram,
                        ipAddress);
                }

                throw;
            }
            catch (Exception exception)
            {
                if (createdProgram is not null)
                {
                    await ArchivePreparedProgramBestEffortAsync(
                        userId,
                        createdProgram,
                        ipAddress);
                }

                _logger.LogWarning(
                    exception,
                    "Create/Publish failed for factory target robot {RobotId}.",
                    item.RobotId);

                return new FactoryRunPreparedProgram
                {
                    RobotId = item.RobotId,
                    Error = $"Create/Publish failed: {exception.Message}"
                };
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        return await Task.WhenAll(tasks);
    }

    private async Task ArchivePreparedProgramBestEffortAsync(
        Guid userId,
        FactoryRunPreparedProgram preparedProgram,
        string? ipAddress)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var programService =
                scope.ServiceProvider.GetRequiredService<IRobotProgramService>();

            var archived = await programService.ArchiveAsync(
                userId,
                preparedProgram.RobotId,
                preparedProgram.Program!.Id,
                ipAddress,
                CancellationToken.None);

            if (!archived)
            {
                _logger.LogWarning(
                    "Unable to archive partially prepared factory program {ProgramId} for robot {RobotId}.",
                    preparedProgram.Program!.Id,
                    preparedProgram.RobotId);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to clean up partially prepared factory program {ProgramId} for robot {RobotId}.",
                preparedProgram.Program!.Id,
                preparedProgram.RobotId);
        }
    }
}
