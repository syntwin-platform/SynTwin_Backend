using Syntwin.Application.Commands.Dtos;

namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotCommandService
{
    Task<RobotCommandResponse?> CreateAsync(
        Guid userId,
        Guid robotId,
        CreateRobotCommandRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RobotCommandResponse>?> ListAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default);
}