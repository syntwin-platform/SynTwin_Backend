using Syntwin.Application.FactoryRuns.Dtos;

namespace Syntwin.Application.FactoryRuns.Interfaces;

public interface IFactoryRunService
{
    Task<FactoryRunResponse?> CreateAsync(
        Guid userId,
        CreateFactoryRunRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<FactoryRunResponse?> PrepareAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<FactoryRunResponse?> StartAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<FactoryRunResponse?> CancelAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<FactoryRunResponse?> GetByIdAsync(
        Guid userId,
        Guid factoryRunId,
        CancellationToken cancellationToken = default);
}