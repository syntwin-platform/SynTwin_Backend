using StackExchange.Redis;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Robots;

public sealed class RedisRobotCommandQueue : IRobotCommandQueue
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;

    public RedisRobotCommandQueue(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
        _subscriber = connectionMultiplexer.GetSubscriber();
    }

    public async Task EnqueueAsync(
        RobotCommand command,
        CancellationToken cancellationToken = default)
    {
        var queueKey = command.CommandType == RobotCommandType.EStop
            ? PriorityQueueKey(command.RobotId)
            : NormalQueueKey(command.RobotId);

        await _database.ListLeftPushAsync(
            queueKey,
            command.Id.ToString());

        await _subscriber.PublishAsync(
            RedisChannel.Literal(CommandSignalChannel(command.RobotId, command.CommandType)),
            command.Id.ToString());
    }

    public async Task EnqueueManyAsync(
    IReadOnlyCollection<RobotCommand> commands,
    CancellationToken cancellationToken = default)
    {
        if (commands.Count == 0)
        {
            return;
        }

        await Task.WhenAll(commands.Select(command => EnqueueAsync(command, cancellationToken)));
    }


    public async Task RequeueAsync(
    RobotCommand command,
    CancellationToken cancellationToken = default)
    {
        var queueKey = command.CommandType == RobotCommandType.EStop
            ? PriorityQueueKey(command.RobotId)
            : NormalQueueKey(command.RobotId);

        await _database.ListRightPushAsync(
            queueKey,
            command.Id.ToString());

        await _subscriber.PublishAsync(
            RedisChannel.Literal(CommandSignalChannel(command.RobotId, command.CommandType)),
            command.Id.ToString());
    }
    public async Task<Guid?> DequeueAsync(
        Guid robotId,
        bool safetyOnly = false,
        CancellationToken cancellationToken = default)
    {
        var priorityCommandId = await PopCommandIdAsync(PriorityQueueKey(robotId));

        if (priorityCommandId.HasValue)
        {
            return priorityCommandId.Value;
        }

        if (safetyOnly)
        {
            return null;
        }

        return await PopCommandIdAsync(NormalQueueKey(robotId));
    }

    public async Task<Guid?> DequeueOrWaitAsync(
        Guid robotId,
        bool safetyOnly,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default)
    {
        if (waitTimeout <= TimeSpan.Zero)
        {
            return await DequeueAsync(robotId, safetyOnly, cancellationToken);
        }

        var priorityChannel = RedisChannel.Literal(PrioritySignalChannel(robotId));
        var normalChannel = RedisChannel.Literal(NormalSignalChannel(robotId));
        var commandAvailable = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void HandleCommandSignal(RedisChannel channel, RedisValue value)
        {
            commandAvailable.TrySetResult(true);
        }

        await _subscriber.SubscribeAsync(priorityChannel, HandleCommandSignal);

        if (!safetyOnly)
        {
            await _subscriber.SubscribeAsync(normalChannel, HandleCommandSignal);
        }

        try
        {
            var commandId = await DequeueAsync(
                robotId,
                safetyOnly,
                cancellationToken);

            if (commandId.HasValue)
            {
                return commandId;
            }

            using var cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    var source = (TaskCompletionSource<bool>)state!;
                    source.TrySetCanceled();
                },
                commandAvailable);

            var delayTask = Task.Delay(waitTimeout, CancellationToken.None);
            var completedTask = await Task.WhenAny(commandAvailable.Task, delayTask);

            if (completedTask != commandAvailable.Task)
            {
                return null;
            }

            await commandAvailable.Task;

            return await DequeueAsync(
                robotId,
                safetyOnly,
                cancellationToken);
        }
        finally
        {
            await _subscriber.UnsubscribeAsync(priorityChannel, HandleCommandSignal);

            if (!safetyOnly)
            {
                await _subscriber.UnsubscribeAsync(normalChannel, HandleCommandSignal);
            }
        }
    }

    private async Task<Guid?> PopCommandIdAsync(string queueKey)
    {
        var value = await _database.ListRightPopAsync(queueKey);

        if (!value.HasValue)
        {
            return null;
        }

        return Guid.TryParse(value.ToString(), out var commandId)
            ? commandId
            : null;
    }

    private static string PriorityQueueKey(Guid robotId)
    {
        return $"robot:{robotId}:commands:priority";
    }

    private static string NormalQueueKey(Guid robotId)
    {
        return $"robot:{robotId}:commands:normal";
    }

    private static string CommandSignalChannel(
        Guid robotId,
        RobotCommandType commandType)
    {
        return commandType == RobotCommandType.EStop
            ? PrioritySignalChannel(robotId)
            : NormalSignalChannel(robotId);
    }

    private static string PrioritySignalChannel(Guid robotId)
    {
        return $"robot:{robotId}:commands:priority:signal";
    }

    private static string NormalSignalChannel(Guid robotId)
    {
        return $"robot:{robotId}:commands:normal:signal";
    }
}
