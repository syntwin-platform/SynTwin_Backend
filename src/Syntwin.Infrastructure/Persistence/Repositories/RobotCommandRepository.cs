using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Domain.Entities;
namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotCommandRepository : IRobotCommandRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotCommandRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(RobotCommand command, CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotCommands.AddAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<RobotCommand>> ListByRobotIdAsync(
    Guid robotId,
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.RobotCommands
    .AsNoTracking()
    .Include(command => command.Result)
    .Where(command => command.RobotId == robotId)
    .OrderByDescending(command => command.CreatedAt)
    .ToListAsync(cancellationToken);
    }
    public Task<RobotCommand?> GetByIdAsync(
    Guid commandId,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotCommands
            .Include(command => command.Result)
            .FirstOrDefaultAsync(
                command => command.Id == commandId,
                cancellationToken);
    }

    public Task<RobotCommand?> GetByIdForRobotAsync(
        Guid commandId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotCommands
            .FirstOrDefaultAsync(
                command => command.Id == commandId && command.RobotId == robotId,
                cancellationToken);
    }

    public Task<CommandResult?> GetResultByCommandIdAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.CommandResults
    .AsNoTracking()
    .FirstOrDefaultAsync(result => result.CommandId == commandId, cancellationToken);
    }

    public async Task AddCommandResultAsync(
        CommandResult result,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.CommandResults.AddAsync(result, cancellationToken);
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

}