using StackExchange.Redis;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Infrastructure.Robots;

public sealed class RedisRobotCommandTimeoutScheduler : IRobotCommandTimeoutScheduler
{
    private readonly IDatabase _database;

    public RedisRobotCommandTimeoutScheduler(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task ScheduleAsync(
        RobotCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.TimeoutAt.HasValue)
        {
            await RemoveAsync(command.Id, cancellationToken);
            return;
        }

        await _database.SortedSetAddAsync(
            TimeoutSetKey(),
            command.Id.ToString(),
            command.TimeoutAt.Value.ToUnixTimeMilliseconds());
    }

    public async Task<IReadOnlyList<Guid>> ListDueCommandIdsAsync(
        DateTimeOffset dueAt,
        int take,
        CancellationToken cancellationToken = default)
    {
        var values = await _database.SortedSetRangeByScoreAsync(
            TimeoutSetKey(),
            double.NegativeInfinity,
            dueAt.ToUnixTimeMilliseconds(),
            Exclude.None,
            Order.Ascending,
            skip: 0,
            take: Math.Max(1, take));

        return values
            .Select(value => Guid.TryParse(value.ToString(), out var commandId)
                ? commandId
                : (Guid?)null)
            .Where(commandId => commandId.HasValue)
            .Select(commandId => commandId!.Value)
            .ToList();
    }

    public async Task RemoveAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await _database.SortedSetRemoveAsync(
            TimeoutSetKey(),
            commandId.ToString());
    }

    private static string TimeoutSetKey()
    {
        return "commands:timeouts";
    }
}