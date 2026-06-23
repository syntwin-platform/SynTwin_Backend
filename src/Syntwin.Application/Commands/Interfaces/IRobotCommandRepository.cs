using Syntwin.Domain.Entities;


namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotCommandRepository
{
    Task AddAsync(RobotCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RobotCommand>> ListByRobotIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<RobotCommand?> GetByIdAsync(
    Guid commandId,
    CancellationToken cancellationToken = default);

    Task<RobotCommand?> GetByIdForRobotAsync(
        Guid commandId,
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<CommandResult?> GetResultByCommandIdAsync(
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task AddCommandResultAsync(
        CommandResult result,
        CancellationToken cancellationToken = default);
}