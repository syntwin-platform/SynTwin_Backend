using Syntwin.Domain.Entities;

namespace Syntwin.Application.RobotPrograms.Interfaces;

public interface IRobotProgramRepository
{
    Task<IReadOnlyList<RobotProgram>> ListByRobotIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<RobotProgram?> GetByIdForRobotAsync(
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RobotProgram program,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}