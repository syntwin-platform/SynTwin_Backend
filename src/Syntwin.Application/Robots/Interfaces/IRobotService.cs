using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotService
{
    Task<CreateRobotResponse> CreateAsync(
        Guid userId,
        CreateRobotRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RobotResponse>> GetMineAsync(
        Guid userId,
        Guid? companyId = null,
        CancellationToken cancellationToken = default);

    Task<RobotResponse?> GetByIdAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<RobotResponse?> UpdateAsync(
        Guid userId,
        Guid robotId,
        UpdateRobotRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<ResetRobotDeviceSecretResponse?> ResetDeviceSecretAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
    Task<RobotLatestStateResponse?> GetLatestStateAsync(
     Guid userId,
     Guid robotId,
     CancellationToken cancellationToken = default);

    Task<RobotRuntimeConfigResponse?> GetRuntimeConfigAsync(
    Guid userId,
    Guid robotId,
    CancellationToken cancellationToken = default);
}
