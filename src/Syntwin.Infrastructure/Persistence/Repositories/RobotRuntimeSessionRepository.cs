using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotRuntimeSessionRepository : IRobotRuntimeSessionRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotRuntimeSessionRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RobotRuntimeSession?> GetOpenByRobotIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotRuntimeSessions
            .FirstOrDefaultAsync(
                session => session.RobotId == robotId && session.EndedAt == null,
                cancellationToken);
    }

    public Task<RobotRuntimeSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotRuntimeSessions
            .FirstOrDefaultAsync(
                session => session.Id == sessionId,
                cancellationToken);
    }

    public async Task AddAsync(
        RobotRuntimeSession session,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotRuntimeSessions.AddAsync(session, cancellationToken);
    }

    public async Task<bool> TryAddOpenSessionAsync(
        RobotRuntimeSession session,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotRuntimeSessions.AddAsync(session, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception) when (IsOpenSessionUniqueConstraintViolation(exception))
        {
            foreach (var entry in exception.Entries)
            {
                entry.State = EntityState.Detached;
            }

            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsOpenSessionUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not SqlException sqlException)
        {
            return false;
        }

        return sqlException.Errors
            .OfType<SqlError>()
            .Any(error =>
                (error.Number == 2601 || error.Number == 2627) &&
                error.Message.Contains(
                    "UX_robot_runtime_sessions_robot_open",
                    StringComparison.OrdinalIgnoreCase));
    }
}
