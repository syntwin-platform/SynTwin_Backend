using Syntwin.Domain.Entities;

namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotCommandTimeoutScheduler
{
    Task ScheduleAsync(
        RobotCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListDueCommandIdsAsync(
        DateTimeOffset dueAt,
        int take,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Guid commandId,
        CancellationToken cancellationToken = default);
}