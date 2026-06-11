using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Domain.Entities;
using System.Data;
using Syntwin.Domain.Enums;
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
            .Where(command => command.RobotId == robotId)
            .OrderByDescending(command => command.CreatedAt)
            .ToListAsync(cancellationToken);
    }
    public async Task<RobotCommand?> TakeNextPendingAsync(
     Guid robotId,
     bool safetyOnly = false,
     CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var pendingCommands = _dbContext.RobotCommands
            .Where(command =>
                command.RobotId == robotId &&
                command.Status == CommandStatus.Pending);

        if (safetyOnly)
        {
            pendingCommands = pendingCommands.Where(
                command => command.CommandType == RobotCommandType.EStop);
        }

        var command = await pendingCommands
            .OrderBy(command =>
                command.CommandType == RobotCommandType.EStop ? 0 : 1)
            .ThenBy(command => command.CreatedAt)
            .ThenBy(command => command.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (command is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        command.Status = CommandStatus.Sent;
        command.SentAt ??= now;
        command.LastDeliveryAttemptAt = now;
        command.DeliveryAttemptCount += 1;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return command;
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

    public async Task<IReadOnlyList<RobotCommand>> ListExpiredActiveCommandsAsync(
    DateTimeOffset now,
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.RobotCommands
            .Where(command =>
                command.TimeoutAt != null &&
                command.TimeoutAt <= now &&
                (command.Status == CommandStatus.Pending ||
                 command.Status == CommandStatus.Sent) &&
                command.Result == null)
            .OrderBy(command => command.TimeoutAt)
            .ToListAsync(cancellationToken);
    }

}