using System.Collections.Concurrent;
using System.Text.Json;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Infrastructure.Persistence;

namespace Syntwin.Integration.Tests.Infrastructure;

public sealed class FactoryRunFailurePlan
{
    public HashSet<Guid> ProgramPreparationFailures { get; } = [];
    public HashSet<Guid> QueueDispatchFailures { get; } = [];

    public void Reset()
    {
        ProgramPreparationFailures.Clear();
        QueueDispatchFailures.Clear();
    }
}

internal sealed class InMemoryDistributedLock : IDistributedLock
{
    public Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IDistributedLockHandle?>(new Handle(key));

    private sealed class Handle(string key) : IDistributedLockHandle
    {
        public string Key { get; } = key;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class InMemoryRobotBusyLock : IRobotBusyLock
{
    private readonly ConcurrentDictionary<Guid, Guid> _owners = new();

    public void Reset() => _owners.Clear();

    public Task<Guid?> GetOwnerAsync(Guid robotId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_owners.TryGetValue(robotId, out var owner) ? (Guid?)owner : null);

    public Task<bool> TryAcquireAsync(
        Guid robotId,
        Guid commandId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_owners.TryAdd(robotId, commandId));

    public Task ReleaseAsync(
        Guid robotId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        if (_owners.TryGetValue(robotId, out var owner) && owner == commandId)
        {
            _owners.TryRemove(robotId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryTransferAsync(
        Guid robotId,
        Guid expectedOwnerId,
        Guid newOwnerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var transferred = _owners.TryUpdate(robotId, newOwnerId, expectedOwnerId);
        return Task.FromResult(transferred);
    }

    public Task<bool> RenewAsync(
        Guid robotId,
        Guid ownerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_owners.TryGetValue(robotId, out var owner) && owner == ownerId);
}

public sealed class RecordingCommandQueue(FactoryRunFailurePlan failurePlan) : IRobotCommandQueue
{
    private readonly ConcurrentQueue<Guid> _commandIds = new();

    public IReadOnlyCollection<Guid> CommandIds => _commandIds.ToArray();

    public void Reset()
    {
        while (_commandIds.TryDequeue(out _))
        {
        }
    }

    public Task EnqueueAsync(RobotCommand command, CancellationToken cancellationToken = default)
    {
        if (failurePlan.QueueDispatchFailures.Contains(command.RobotId))
        {
            throw new InvalidOperationException($"Queue unavailable for robot {command.RobotId}.");
        }

        _commandIds.Enqueue(command.Id);
        return Task.CompletedTask;
    }

    public async Task EnqueueManyAsync(
        IReadOnlyCollection<RobotCommand> commands,
        CancellationToken cancellationToken = default)
    {
        foreach (var command in commands)
        {
            await EnqueueAsync(command, cancellationToken);
        }
    }

    public Task RequeueAsync(RobotCommand command, CancellationToken cancellationToken = default) =>
        EnqueueAsync(command, cancellationToken);

    public Task<Guid?> DequeueAsync(
        Guid robotId,
        bool safetyOnly = false,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);

    public Task<Guid?> DequeueOrWaitAsync(
        Guid robotId,
        bool safetyOnly,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Guid?>(null);
}

public sealed class RecordingCommandTimeoutScheduler : IRobotCommandTimeoutScheduler
{
    private readonly ConcurrentDictionary<Guid, byte> _scheduled = new();

    public IReadOnlyCollection<Guid> ScheduledIds => _scheduled.Keys.ToArray();

    public void Reset() => _scheduled.Clear();

    public Task ScheduleAsync(RobotCommand command, CancellationToken cancellationToken = default)
    {
        _scheduled[command.Id] = 0;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> ListDueCommandIdsAsync(
        DateTimeOffset dueAt,
        int take,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    public Task RemoveAsync(Guid commandId, CancellationToken cancellationToken = default)
    {
        _scheduled.TryRemove(commandId, out _);
        return Task.CompletedTask;
    }
}

internal sealed class DatabaseProgramPreparationExecutor(
    SyntwinDbContext dbContext,
    FactoryRunFailurePlan failurePlan) : IFactoryRunProgramPreparationExecutor
{
    public async Task<IReadOnlyList<FactoryRunPreparedProgram>> CreateAndPublishManyAsync(
        Guid userId,
        IReadOnlyCollection<FactoryRunProgramPreparationItem> items,
        string? ipAddress,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FactoryRunPreparedProgram>(items.Count);

        foreach (var item in items)
        {
            if (failurePlan.ProgramPreparationFailures.Contains(item.RobotId))
            {
                results.Add(new FactoryRunPreparedProgram
                {
                    RobotId = item.RobotId,
                    Error = "Injected preparation failure."
                });
                continue;
            }

            var request = item.CreateProgramRequest;
            var program = new RobotProgram
            {
                RobotId = item.RobotId,
                Name = request.Name,
                Status = RobotProgramStatus.Published,
                Source = RobotProgramSource.ImportedLua,
                CreatedByUserId = userId,
                Steps = request.Steps.Select(step => new RobotProgramStep
                {
                    OrderIndex = step.OrderIndex,
                    StepType = Enum.Parse<RobotProgramStepType>(step.StepType, true),
                    Label = step.Label,
                    PayloadJson = step.Payload.GetRawText()
                }).ToList()
            };

            dbContext.RobotPrograms.Add(program);
            await dbContext.SaveChangesAsync(cancellationToken);

            results.Add(new FactoryRunPreparedProgram
            {
                RobotId = item.RobotId,
                Program = new RobotProgramResponse
                {
                    Id = program.Id,
                    RobotId = program.RobotId,
                    Name = program.Name,
                    Status = program.Status.ToString(),
                    Source = program.Source.ToString(),
                    CreatedByUserId = program.CreatedByUserId,
                    CreatedAt = program.CreatedAt,
                    Steps = program.Steps
                        .OrderBy(step => step.OrderIndex)
                        .Select(step => new RobotProgramStepResponse
                        {
                            Id = step.Id,
                            OrderIndex = step.OrderIndex,
                            StepType = step.StepType.ToString(),
                            Label = step.Label,
                            Payload = JsonSerializer.Deserialize<JsonElement>(step.PayloadJson),
                            CreatedAt = step.CreatedAt
                        })
                        .ToList()
                }
            });
        }

        return results;
    }
}
