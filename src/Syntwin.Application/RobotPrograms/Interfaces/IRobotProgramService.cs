using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.RobotPrograms.Interfaces;

public interface IRobotProgramService
{
    Task<IReadOnlyList<RobotProgramResponse>?> ListAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<RobotProgramResponse?> GetByIdAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default);

    Task<RobotProgramResponse?> CreateAsync(
     Guid userId,
     Guid robotId,
     CreateRobotProgramRequest request,
     string? ipAddress,
     CancellationToken cancellationToken = default);

    Task<RobotProgramResponse?> UpdateAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        UpdateRobotProgramRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<RobotProgramResponse?> PublishAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> ArchiveAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<LuaExportResponse?> ExportLuaAsync(
    Guid userId,
    Guid robotId,
    Guid programId,
    CancellationToken cancellationToken = default);
}