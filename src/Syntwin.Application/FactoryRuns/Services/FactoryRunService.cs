using Syntwin.Application.Commands.Interfaces;
using Microsoft.Extensions.Options;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.FactoryRuns.Strategies;
using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Syntwin.Application.FactoryRuns.Services;

public sealed class FactoryRunService : IFactoryRunService
{
    private const int MaxLuaContentBytes = 1024 * 1024;
    private const int MaxTotalLuaContentBytes = 5 * 1024 * 1024;
    private const int MaxProgramCount = 20;
    private const int MaxTargetCount = 20;
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromMinutes(5);
    private readonly IFactoryRunRepository _factoryRunRepository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotAccessService _robotAccessService;
    private readonly ILuaProgramImportService _luaProgramImportService;
    private readonly IFactoryRunProgramPreparationExecutor _programPreparationExecutor;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IRobotCommandQueue _commandQueue;
    private readonly IRobotCommandTimeoutScheduler _commandTimeoutScheduler;
    private readonly IRobotBusyLock _robotBusyLock;
    private readonly IRobotRuntimeMetrics _metrics;
    private readonly FactoryRunExecutionStrategyResolver _executionStrategyResolver;
    private readonly TimeSpan _factoryRunBusyLockTtl;


    public FactoryRunService(
        IFactoryRunRepository factoryRunRepository,
        ICompanyRepository companyRepository,
        IRobotRepository robotRepository,
        IRobotAccessService robotAccessService,
        ILuaProgramImportService luaProgramImportService,
        IFactoryRunProgramPreparationExecutor programPreparationExecutor,
        IRobotCommandRepository commandRepository,
        IRobotCommandQueue commandQueue,
        IRobotCommandTimeoutScheduler commandTimeoutScheduler,
        IRobotBusyLock robotBusyLock,
        IRobotRuntimeMetrics metrics,
        FactoryRunExecutionStrategyResolver executionStrategyResolver,
        IOptions<RobotRuntimeOptions> runtimeOptions)
    {
        _factoryRunRepository = factoryRunRepository;
        _companyRepository = companyRepository;
        _robotRepository = robotRepository;
        _robotAccessService = robotAccessService;
        _luaProgramImportService = luaProgramImportService;
        _programPreparationExecutor = programPreparationExecutor;
        _commandRepository = commandRepository;
        _commandQueue = commandQueue;
        _commandTimeoutScheduler = commandTimeoutScheduler;
        _robotBusyLock = robotBusyLock;
        _metrics = metrics;
        _executionStrategyResolver = executionStrategyResolver;
        _factoryRunBusyLockTtl = TimeSpan.FromSeconds(Math.Max(
            60,
            runtimeOptions.Value.FactoryRunBusyLockTtlSeconds));
    }

    public async Task<FactoryRunResponse?> CreateAsync(
        Guid userId,
        CreateFactoryRunRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeCreateRequest(request);

        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);
        if (company is null || company.Status != CompanyStatus.Active)
        {
            return null;
        }

        if (!await HasCompanyAccessAsync(userId, request.CompanyId, cancellationToken))
        {
            return null;
        }

        var robotIds = normalizedRequest.Targets
            .Select(target => target.RobotId)
            .ToArray();

        var robots = await _robotRepository.ListByIdsAsync(robotIds, cancellationToken);
        var robotsById = robots.ToDictionary(robot => robot.Id);

        foreach (var robotId in robotIds)
        {
            if (!robotsById.TryGetValue(robotId, out var robot))
            {
                throw new InvalidOperationException($"Robot {robotId} was not found.");
            }

            if (robot.CompanyId != request.CompanyId)
            {
                throw new InvalidOperationException($"Robot {robotId} does not belong to the selected company.");
            }

            if (robot.Status == RobotStatus.Disabled)
            {
                throw new InvalidOperationException($"Robot {robot.RobotName} is disabled.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var factoryRunId = Guid.NewGuid();
        var programByHash = new Dictionary<string, FactoryRunProgram>(
            StringComparer.OrdinalIgnoreCase);
        var programByKey = new Dictionary<string, FactoryRunProgram>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var programRequest in normalizedRequest.Programs)
        {
            var contentHash = ComputeSha256(programRequest.LuaContent);

            if (!programByHash.TryGetValue(contentHash, out var sourceProgram))
            {
                sourceProgram = new FactoryRunProgram
                {
                    Id = Guid.NewGuid(),
                    FactoryRunId = factoryRunId,
                    ProgramKey = programRequest.Key,
                    ProgramName = programRequest.ProgramName,
                    LuaFileName = programRequest.LuaFileName,
                    LuaContent = programRequest.LuaContent,
                    LuaContentHash = contentHash,
                    CreatedAtUtc = now
                };

                programByHash.Add(contentHash, sourceProgram);
            }

            programByKey.Add(programRequest.Key, sourceProgram);
        }

        var primaryProgram = programByKey[normalizedRequest.Programs[0].Key];
        var factoryRun = new FactoryRun
        {
            Id = factoryRunId,
            CompanyId = request.CompanyId,
            CreatedByUserId = userId,
            Status = FactoryRunStatus.Created,
            CoordinationMode = request.CoordinationMode,
            FailurePolicy = request.FailurePolicy,
            // Legacy projection retained during the compatibility window.
            ProgramName = primaryProgram.ProgramName,
            LuaFileName = primaryProgram.LuaFileName,
            LuaContent = primaryProgram.LuaContent,
            LuaContentHash = primaryProgram.LuaContentHash,
            TargetCount = robotIds.Length,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Programs = programByHash.Values.ToList(),
            Targets = normalizedRequest.Targets
                .Select(targetRequest =>
                {
                    var sourceProgram = programByKey[targetRequest.ProgramKey];

                    return new FactoryRunTarget
                    {
                        Id = Guid.NewGuid(),
                        FactoryRunId = factoryRunId,
                        RobotId = targetRequest.RobotId,
                        FactoryRunProgramId = sourceProgram.Id,
                        FactoryRunProgram = sourceProgram,
                        Status = FactoryRunTargetStatus.Pending,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                })
                .ToList()
        };

        await _factoryRunRepository.AddAsync(factoryRun, cancellationToken);
        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(factoryRun);
    }

    public async Task<FactoryRunResponse?> PrepareAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var factoryRun = await GetAuthorizedFactoryRunAsync(
            userId,
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        var statusBeforeRefresh = factoryRun.Status;
        var refreshed = await RefreshFromCommandsAsync(factoryRun, cancellationToken);

        if (refreshed)
        {
            RecordFactoryRunOutcomeTransition(statusBeforeRefresh, factoryRun);
        }

        if (factoryRun.Status is FactoryRunStatus.Ready or FactoryRunStatus.WaitingForReady)
        {
            return ToResponse(factoryRun);
        }

        if (factoryRun.Status != FactoryRunStatus.Created)
        {
            throw new InvalidOperationException("Only a newly created factory run can be prepared.");
        }

        var preparationStopwatch = Stopwatch.StartNew();
        var preparationSucceeded = false;

        var now = DateTimeOffset.UtcNow;
        var acquiredLocks = new List<(Guid RobotId, Guid LockOwnerId)>();
        var prepareCommandsToDispatch = new List<RobotCommand>();
        var prepareCommandsPersisted = false;

        factoryRun.Status = FactoryRunStatus.Preparing;
        factoryRun.UpdatedAtUtc = now;

        foreach (var target in factoryRun.Targets)
        {
            target.Status = FactoryRunTargetStatus.Preparing;
            target.PrepareStartedAtUtc = now;
            target.UpdatedAtUtc = now;
        }

        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        try
        {
            foreach (var target in factoryRun.Targets.OrderBy(target => target.CreatedAtUtc))
            {
                var prepareLockOwnerId = target.Id;

                var lockAcquired = await _robotBusyLock.TryAcquireAsync(
                    target.RobotId,
                    prepareLockOwnerId,
                    _factoryRunBusyLockTtl,
                    cancellationToken);

                if (!lockAcquired)
                {
                    target.Status = FactoryRunTargetStatus.Failed;
                    target.TerminationReason = FactoryRunTargetTerminationReason.CommandFailure;
                    target.ReadinessError = "Robot already has an active command.";
                    target.FailureReason = "Robot already has an active command.";
                    target.UpdatedAtUtc = DateTimeOffset.UtcNow;

                    if (factoryRun.FailurePolicy == FactoryFailurePolicy.AbortExecutionGroup)
                    {
                        throw new InvalidOperationException(
                            $"Robot {target.RobotId} already has an active command.");
                    }

                    continue;
                }

                acquiredLocks.Add((target.RobotId, prepareLockOwnerId));
            }

            var acquiredRobotIds = acquiredLocks
                .Select(item => item.RobotId)
                .ToHashSet();
            var activeTargets = factoryRun.Targets
                .Where(target => acquiredRobotIds.Contains(target.RobotId))
                .OrderBy(target => target.CreatedAtUtc)
                .ToList();

            if (activeTargets.Count == 0)
            {
                throw new InvalidOperationException(
                    "No robot target was available for FactoryRun preparation.");
            }

            var orderedTargets = activeTargets;
            var preparationItems = new List<FactoryRunProgramPreparationItem>(
                orderedTargets.Count);
            var preflightFailures = new Dictionary<Guid, string>();

            // Parse once per deduplicated source Lua, then clone the resulting
            // program request for every target assigned to that source.
            foreach (var sourceGroup in orderedTargets.GroupBy(target =>
                target.FactoryRunProgramId ?? Guid.Empty))
            {
                var representativeTarget = sourceGroup.First();
                var sourceProgram = representativeTarget.FactoryRunProgram;
                var importRequest = new LuaParseRequest
                {
                    FileName = sourceProgram?.LuaFileName ?? factoryRun.LuaFileName,
                    LuaContent = sourceProgram?.LuaContent ?? factoryRun.LuaContent
                };

                var importPreview = await _luaProgramImportService.PreviewAsync(
                    userId,
                    representativeTarget.RobotId,
                    importRequest,
                    cancellationToken);

                var previewError = importPreview is null
                    ? "Robot not found or access denied."
                    : importPreview.CreateProgramRequest is null
                        ? importPreview.Diagnostics
                            .FirstOrDefault(diagnostic => diagnostic.Severity == "error")
                            ?.Message ??
                            "The LUA file does not contain any valid robot program steps."
                        : null;

                if (previewError is not null)
                {
                    foreach (var target in sourceGroup)
                    {
                        preflightFailures[target.RobotId] = previewError;
                    }

                    if (factoryRun.FailurePolicy ==
                        FactoryFailurePolicy.AbortExecutionGroup)
                    {
                        throw new InvalidOperationException(previewError);
                    }

                    continue;
                }

                foreach (var target in sourceGroup)
                {
                    preparationItems.Add(new FactoryRunProgramPreparationItem
                    {
                        RobotId = target.RobotId,
                        CreateProgramRequest = CloneCreateProgramRequest(
                            importPreview!.CreateProgramRequest!)
                    });
                }
            }

            var preparationResults =
                await _programPreparationExecutor.CreateAndPublishManyAsync(
                    userId,
                    preparationItems,
                    ipAddress,
                    maxConcurrency: 3,
                    cancellationToken: cancellationToken);

            var preparationResultsByRobotId = preparationResults
                .ToDictionary(result => result.RobotId);

            foreach (var preflightFailure in preflightFailures)
            {
                preparationResultsByRobotId.Add(
                    preflightFailure.Key,
                    new FactoryRunPreparedProgram
                    {
                        RobotId = preflightFailure.Key,
                        Error = preflightFailure.Value
                    });
            }

            var preparedPrograms =
                new List<(FactoryRunTarget Target, RobotProgramResponse Program)>(
                    orderedTargets.Count);
            var isolatedPreparationLocks = new List<(Guid RobotId, Guid OwnerId)>();

            foreach (var target in orderedTargets)
            {
                if (!preparationResultsByRobotId.TryGetValue(
                        target.RobotId,
                        out var result))
                {
                    throw new InvalidOperationException(
                        $"No prepared program was returned for robot {target.RobotId}.");
                }

                if (!result.IsSuccess || result.Program is null)
                {
                    var error = result.Error ?? "Create/Publish failed.";

                    target.Status = FactoryRunTargetStatus.Failed;
                    target.TerminationReason =
                        FactoryRunTargetTerminationReason.CommandFailure;
                    target.ReadinessError = error;
                    target.FailureReason = error;
                    target.CompletedAtUtc = DateTimeOffset.UtcNow;
                    target.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    isolatedPreparationLocks.Add((target.RobotId, target.Id));

                    if (factoryRun.FailurePolicy ==
                        FactoryFailurePolicy.AbortExecutionGroup)
                    {
                        throw new InvalidOperationException(
                            $"Create/Publish failed for robot {target.RobotId}: {error}");
                    }

                    continue;
                }

                var publishedProgram = result.Program;

                target.ProgramId = publishedProgram.Id;
                target.Status = FactoryRunTargetStatus.Prepared;
                target.PreparedAtUtc = DateTimeOffset.UtcNow;
                target.ReadinessError = null;
                target.FailureReason = null;
                target.UpdatedAtUtc = DateTimeOffset.UtcNow;

                preparedPrograms.Add((target, publishedProgram));
            }

            if (preparedPrograms.Count == 0)
            {
                throw new InvalidOperationException(
                    "No FactoryRun target completed program preparation.");
            }

            // Persist the isolated target first. A crash after this point leaves a
            // terminal DB record, so the maintenance worker can safely repeat the
            // owner-aware Redis cleanup.
            if (isolatedPreparationLocks.Count > 0)
            {
                await _factoryRunRepository.SaveChangesAsync(cancellationToken);

                foreach (var isolatedLock in isolatedPreparationLocks)
                {
                    await _robotBusyLock.ReleaseAsync(
                        isolatedLock.RobotId,
                        isolatedLock.OwnerId,
                        cancellationToken);
                    acquiredLocks.Remove(isolatedLock);
                }
            }

            foreach (var (target, publishedProgram) in preparedPrograms)
            {
                var prepareCommand = CreateFactoryRunPrepareCommand(
                    userId,
                    target.RobotId,
                    factoryRun.Id,
                    target.Id,
                    publishedProgram.Id,
                    publishedProgram.Name);

                target.PrepareCommandId = prepareCommand.Id;
                target.Status = FactoryRunTargetStatus.WaitingForDeviceReady;
                target.UpdatedAtUtc = DateTimeOffset.UtcNow;

                await _commandRepository.AddAsync(prepareCommand, cancellationToken);
                prepareCommandsToDispatch.Add(prepareCommand);
            }

            factoryRun.Status = FactoryRunStatus.WaitingForReady;
            factoryRun.PreparedAtUtc = DateTimeOffset.UtcNow;
            factoryRun.FailureReason = null;
            factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _factoryRunRepository.SaveChangesAsync(cancellationToken);
            prepareCommandsPersisted = true;
            await Task.WhenAll(
                prepareCommandsToDispatch.Select(command =>
                    _commandTimeoutScheduler.ScheduleAsync(
                        command,
                        cancellationToken)));

            await _commandQueue.EnqueueManyAsync(
                prepareCommandsToDispatch,
                cancellationToken);

            preparationSucceeded = true;
            return ToResponse(factoryRun);
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.UtcNow;
            var cleanupToken = exception is OperationCanceledException
                ? CancellationToken.None
                : cancellationToken;

            // Chỉ cleanup command khi lần SaveChanges tạo command đã thành công.
            // Nếu lỗi xảy ra trước đó thì danh sách có thể chưa tồn tại trong DB.
            if (prepareCommandsPersisted)
            {
                foreach (var command in prepareCommandsToDispatch)
                {
                    if (command.Status is CommandStatus.Pending or CommandStatus.Sent)
                    {
                        command.Status = CommandStatus.Cancelled;
                        command.CompletedAt = failedAt;
                        command.TimeoutAt = null;
                        command.FailureReason =
                            "Factory run prepare dispatch failed before all devices received the command.";
                    }
                }
            }

            factoryRun.Status = FactoryRunStatus.Failed;
            factoryRun.FailureReason = exception.Message;
            factoryRun.UpdatedAtUtc = failedAt;

            foreach (var target in factoryRun.Targets)
            {
                if (target.Status is not FactoryRunTargetStatus.Failed)
                {
                    target.Status = FactoryRunTargetStatus.Failed;
                    target.FailureReason = exception.Message;
                    target.ReadinessError = exception.Message;
                    target.UpdatedAtUtc = failedAt;
                }
            }

            await _factoryRunRepository.SaveChangesAsync(cleanupToken);

            foreach (var (robotId, lockOwnerId) in acquiredLocks)
            {
                await _robotBusyLock.ReleaseAsync(
                    robotId,
                    lockOwnerId,
                    cleanupToken);
            }

            _metrics.RecordFactoryRunOutcome(
                "prepare_failed",
                factoryRun.Targets.Count);

            // DB đã là terminal trước khi cleanup Redis.
            // Queue item cũ vẫn an toàn vì dequeue sẽ bỏ command không còn Pending.
            if (prepareCommandsPersisted)
            {
                foreach (var command in prepareCommandsToDispatch)
                {
                    try
                    {
                        await _commandTimeoutScheduler.RemoveAsync(
                            command.Id,
                            CancellationToken.None);
                    }
                    catch
                    {
                        // Best effort:
                        // command.TimeoutAt đã null và status đã terminal trong DB.
                        // Timeout worker sẽ loại entry Redis còn sót khi gặp lại.
                    }
                }
            }

            return ToResponse(factoryRun);
        }
        finally
        {
            preparationStopwatch.Stop();

            _metrics.RecordFactoryRunPreparation(
                preparationStopwatch.Elapsed.TotalMilliseconds,
                factoryRun.Targets.Count,
                preparationSucceeded);
        }
    }


    public async Task<FactoryRunResponse?> StartAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var factoryRun = await GetAuthorizedFactoryRunForStartAsync(
            userId,
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        var statusBeforeRefresh = factoryRun.Status;

        if (await RefreshFromCommandsAsync(factoryRun, cancellationToken))
        {
            RecordFactoryRunOutcomeTransition(statusBeforeRefresh, factoryRun);
        }

        if (factoryRun.Status != FactoryRunStatus.Ready)
        {
            throw new InvalidOperationException("Factory run can only start after all devices report Ready.");
        }

        var executionStrategy = _executionStrategyResolver.Resolve(
            factoryRun.CoordinationMode);
        var participatingTargets = executionStrategy.SelectStartTargets(factoryRun);
        var notReadyTarget = participatingTargets.FirstOrDefault(target =>
            target.Status != FactoryRunTargetStatus.Ready);

        if (notReadyTarget is not null)
        {
            throw new InvalidOperationException($"Robot target {notReadyTarget.RobotId} is not Ready.");
        }

        if (participatingTargets.Count == 0)
        {
            throw new InvalidOperationException("Factory run has no Ready target to start.");
        }

        // Build and validate every snapshot before changing the persisted run state.
        // In particular, do not assign FactoryRunTarget.CommandId until the matching
        // RobotCommand has been created and tracked by EF; otherwise an exception in
        // snapshot creation leaves an orphan foreign key in the change tracker.
        var preparedCommands = participatingTargets
            .OrderBy(target => target.CreatedAtUtc)
            .Select(target =>
            {
                if (!target.ProgramId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Factory run target has no prepared program.");
                }

                if (target.CommandId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Factory run target {target.Id} already has a run command.");
                }

                return (
                    Target: target,
                    CommandId: Guid.NewGuid(),
                    PayloadJson: BuildFactoryRunSnapshotPayload(
                        target,
                        factoryRun.Id,
                        executionStrategy.Mode,
                        factoryRun.FailurePolicy,
                        scheduledStartAtUtc: null));
            })
            .ToList();

        var now = DateTimeOffset.UtcNow;

        factoryRun.Status = FactoryRunStatus.Starting;
        factoryRun.ScheduledStartAtUtc = null;
        factoryRun.StartedAtUtc = null;
        factoryRun.UpdatedAtUtc = now;

        foreach (var target in participatingTargets)
        {
            target.Status = FactoryRunTargetStatus.Starting;
            target.UpdatedAtUtc = now;
        }

        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        var commandsToDispatch = new List<RobotCommand>();
        var acquiredRunLocks = new List<(Guid RobotId, Guid CommandId)>();

        try
        {
            foreach (var preparedCommand in preparedCommands)
            {
                var target = preparedCommand.Target;
                var runCommandId = preparedCommand.CommandId;

                var runLockAcquired = await _robotBusyLock.TryTransferAsync(
                    target.RobotId,
                    target.Id,
                    runCommandId,
                    _factoryRunBusyLockTtl,
                    cancellationToken);

                if (!runLockAcquired)
                {
                    target.Status = FactoryRunTargetStatus.Failed;
                    target.TerminationReason = FactoryRunTargetTerminationReason.CommandFailure;
                    target.FailureReason = "Robot no longer owns its preparation lock.";
                    target.UpdatedAtUtc = DateTimeOffset.UtcNow;

                    if (factoryRun.FailurePolicy == FactoryFailurePolicy.AbortExecutionGroup)
                    {
                        throw new InvalidOperationException(
                            $"Robot {target.RobotId} no longer owns its preparation lock.");
                    }

                    continue;
                }

                acquiredRunLocks.Add((target.RobotId, runCommandId));

                var command = new RobotCommand
                {
                    Id = runCommandId,
                    RobotId = target.RobotId,
                    UserId = userId,
                    CommandType = RobotCommandType.RunProgram,
                    PayloadJson = preparedCommand.PayloadJson,
                    Status = CommandStatus.Pending,
                    CreatedAt = now,
                    TimeoutAt = now.Add(DefaultCommandTimeout)

                };

                await _commandRepository.AddAsync(command, cancellationToken);
                commandsToDispatch.Add(command);

                // Set both sides after the principal command is tracked so EF can
                // insert robot_commands before updating the target foreign key.
                target.Command = command;
                target.CommandId = command.Id;

                target.Status = FactoryRunTargetStatus.Starting;
                target.StartedAtUtc = null;
                target.CommandReceivedAtUtc = null;
                target.ArmedAtUtc = null;
                target.TerminationReason = null;
                target.FailureReason = null;
                target.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            if (commandsToDispatch.Count == 0)
            {
                throw new InvalidOperationException(
                    "No FactoryRun target retained its preparation lock at start time.");
            }

            factoryRun.Status = FactoryRunStatus.Starting;
            factoryRun.StartedAtUtc = null;
            factoryRun.FailureReason = commandsToDispatch.Count < factoryRun.Targets.Count
                ? $"Factory run is starting with {commandsToDispatch.Count}/" +
                  $"{factoryRun.Targets.Count} surviving targets."
                : null;
            factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _factoryRunRepository.SaveChangesAsync(cancellationToken);

            if (
                executionStrategy.Mode == FactoryCoordinationMode.ParallelIndependent &&
                factoryRun.FailurePolicy == FactoryFailurePolicy.IsolateTarget)
            {
                var dispatchedCount = await DispatchIndependentCommandsAsync(
                    factoryRun,
                    commandsToDispatch,
                    cancellationToken);

                if (dispatchedCount == 0)
                {
                    throw new InvalidOperationException(
                        "No independent FactoryRun command could be dispatched.");
                }
            }
            else
            {
                await Task.WhenAll(commandsToDispatch.Select(command =>
                    _commandTimeoutScheduler.ScheduleAsync(command, cancellationToken)));

                await _commandQueue.EnqueueManyAsync(commandsToDispatch, cancellationToken);
            }

            return ToResponse(factoryRun);
        }
        catch (Exception exception)
        {
            var failedAt = DateTimeOffset.UtcNow;
            var cleanupToken = exception is OperationCanceledException
                ? CancellationToken.None
                : cancellationToken;

            // Commands created in this request were never fully dispatched. Keeping
            // them as Cancelled preserves referential integrity if the failure state
            // is saved while preventing a later device poll from executing them.
            foreach (var command in commandsToDispatch)
            {
                command.Status = CommandStatus.Cancelled;
                command.CompletedAt = failedAt;
                command.FailureReason =
                    "Factory run start failed before all commands were dispatched.";

                await _commandTimeoutScheduler.RemoveAsync(
                    command.Id,
                    cleanupToken);
            }

            foreach (var target in factoryRun.Targets)
            {
                if (target.Status is not FactoryRunTargetStatus.Failed)
                {
                    target.Status = FactoryRunTargetStatus.Failed;
                    target.TerminationReason = FactoryRunTargetTerminationReason.CommandFailure;
                    target.FailureReason = "Factory run start failed before all commands were dispatched.";
                    target.UpdatedAtUtc = failedAt;
                }
            }

            factoryRun.Status = FactoryRunStatus.Failed;
            factoryRun.FailureReason = exception.Message;
            factoryRun.UpdatedAtUtc = failedAt;

            await _factoryRunRepository.SaveChangesAsync(cleanupToken);

            foreach (var target in factoryRun.Targets)
            {
                await _robotBusyLock.ReleaseAsync(
                    target.RobotId,
                    target.Id,
                    cleanupToken);
            }

            foreach (var (robotId, commandId) in acquiredRunLocks)
            {
                await _robotBusyLock.ReleaseAsync(
                    robotId,
                    commandId,
                    cleanupToken);
            }

            _metrics.RecordFactoryRunOutcome(
                "dispatch_failed",
                factoryRun.Targets.Count);

            return ToResponse(factoryRun);
        }
    }


    public async Task<FactoryRunResponse?> CancelAsync(
        Guid userId,
        Guid factoryRunId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var factoryRun = await GetAuthorizedFactoryRunAsync(
            userId,
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        var statusBeforeRefresh = factoryRun.Status;
        var refreshed = await RefreshFromCommandsAsync(factoryRun, cancellationToken);

        if (factoryRun.Status is
            FactoryRunStatus.Completed or
            FactoryRunStatus.PartiallyCompleted or
            FactoryRunStatus.Failed or
            FactoryRunStatus.Cancelled)
        {
            if (refreshed)
            {
                RecordFactoryRunOutcomeTransition(statusBeforeRefresh, factoryRun);
            }
            return ToResponse(factoryRun);
        }

        var now = DateTimeOffset.UtcNow;
        var shouldSendEStop = factoryRun.Targets.Any(target =>
    target.Status is FactoryRunTargetStatus.Starting or FactoryRunTargetStatus.Armed or FactoryRunTargetStatus.Running);

        factoryRun.Status = shouldSendEStop
            ? FactoryRunStatus.Cancelling
            : FactoryRunStatus.Cancelled;

        factoryRun.CancelledAtUtc = shouldSendEStop ? null : now;
        factoryRun.FailureReason = shouldSendEStop
            ? "Cancel requested. EStop commands were queued for running robots."
            : "Factory run was cancelled before start.";
        factoryRun.UpdatedAtUtc = now;

        foreach (var target in factoryRun.Targets)
        {
            if (target.Status is FactoryRunTargetStatus.Completed or FactoryRunTargetStatus.Failed or FactoryRunTargetStatus.Cancelled)
            {
                continue;
            }

            if (shouldSendEStop &&
                target.Status is (FactoryRunTargetStatus.Starting or FactoryRunTargetStatus.Armed or FactoryRunTargetStatus.Running))
            {
                var eStopCommand = new RobotCommand
                {
                    Id = Guid.NewGuid(),
                    RobotId = target.RobotId,
                    UserId = userId,
                    CommandType = RobotCommandType.EStop,
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        factoryRunId = factoryRun.Id,
                        reason = "FactoryRun cancel requested"
                    }),
                    Status = CommandStatus.Pending,
                    CreatedAt = now,
                    TimeoutAt = now.Add(DefaultCommandTimeout)
                };

                await _commandRepository.AddAsync(eStopCommand, cancellationToken);
                await _factoryRunRepository.SaveChangesAsync(cancellationToken);
                await _commandQueue.EnqueueAsync(eStopCommand, cancellationToken);

                target.FailureReason = "Cancel requested; EStop queued.";
                target.UpdatedAtUtc = now;
            }
            else
            {
                target.Status = FactoryRunTargetStatus.Cancelled;
                target.TerminationReason = FactoryRunTargetTerminationReason.UserCancelled;
                target.CompletedAtUtc = now;
                target.FailureReason = "Cancelled before start.";

                await _robotBusyLock.ReleaseAsync(
                    target.RobotId,
                    target.Id,
                    cancellationToken);

                if (target.CommandId.HasValue)
                {
                    await _robotBusyLock.ReleaseAsync(
                        target.RobotId,
                        target.CommandId.Value,
                        cancellationToken);
                }

                target.UpdatedAtUtc = now;
            }
        }

        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        if (factoryRun.Status == FactoryRunStatus.Cancelled)
        {
            _metrics.RecordFactoryRunOutcome(
                "cancelled_by_user",
                factoryRun.Targets.Count);
        }

        return ToResponse(factoryRun);
    }


    public async Task<FactoryRunResponse?> GetByIdAsync(
        Guid userId,
        Guid factoryRunId,
        CancellationToken cancellationToken = default)
    {
        var factoryRun = await GetAuthorizedFactoryRunAsync(
            userId,
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        var statusBeforeRefresh = factoryRun.Status;

        if (await RefreshFromCommandsAsync(factoryRun, cancellationToken))
        {
            RecordFactoryRunOutcomeTransition(statusBeforeRefresh, factoryRun);
        }

        return ToResponse(factoryRun);
    }

    private async Task<FactoryRun?> GetAuthorizedFactoryRunAsync(
        Guid userId,
        Guid factoryRunId,
        CancellationToken cancellationToken)
    {
        var factoryRun = await _factoryRunRepository.GetByIdAsync(
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        return await HasCompanyAccessAsync(userId, factoryRun.CompanyId, cancellationToken)
            ? factoryRun
            : null;
    }

    private async Task<FactoryRun?> GetAuthorizedFactoryRunForStartAsync(
    Guid userId,
    Guid factoryRunId,
    CancellationToken cancellationToken)
    {
        var factoryRun = await _factoryRunRepository.GetByIdForStartAsync(
            factoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            return null;
        }

        return await HasCompanyAccessAsync(userId, factoryRun.CompanyId, cancellationToken)
            ? factoryRun
            : null;
    }
    private async Task<bool> HasCompanyAccessAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var role = await _robotAccessService.GetCompanyRoleAsync(
            userId,
            companyId,
            cancellationToken);

        return role.HasValue;
    }

    private static NormalizedFactoryRunCreateRequest NormalizeCreateRequest(
        CreateFactoryRunRequest request)
    {
        if (request.CompanyId == Guid.Empty)
        {
            throw new InvalidOperationException("CompanyId is required.");
        }
        if (!Enum.IsDefined(request.CoordinationMode))
        {
            throw new InvalidOperationException(
                $"Unsupported FactoryRun coordination mode: {request.CoordinationMode}.");
        }

        if (!Enum.IsDefined(request.FailurePolicy))
        {
            throw new InvalidOperationException(
                $"Unsupported FactoryRun failure policy: {request.FailurePolicy}.");
        }

        var requestedPrograms = request.Programs ?? [];
        var requestedTargets = request.Targets ?? [];
        var usesPerTargetContract =
            requestedPrograms.Count > 0 || requestedTargets.Count > 0;

        List<NormalizedFactoryRunProgramRequest> programs;
        List<NormalizedFactoryRunTargetRequest> targets;

        if (usesPerTargetContract)
        {
            if (requestedPrograms.Count == 0 || requestedTargets.Count == 0)
            {
                throw new InvalidOperationException(
                    "Programs and Targets must both be provided for per-target FactoryRun requests.");
            }

            if (requestedPrograms.Any(program => program is null))
            {
                throw new InvalidOperationException(
                    "FactoryRun programs must not contain null entries.");
            }

            if (requestedTargets.Any(target => target is null))
            {
                throw new InvalidOperationException(
                    "FactoryRun targets must not contain null entries.");
            }

            programs = requestedPrograms
                .Select(program => NormalizeProgramRequest(
                    program.Key,
                    program.ProgramName,
                    program.LuaFileName,
                    program.LuaContent))
                .ToList();

            targets = requestedTargets
                .Select(target => new NormalizedFactoryRunTargetRequest(
                    target.RobotId,
                    target.ProgramKey?.Trim() ?? string.Empty))
                .ToList();
        }
        else
        {
            var legacyProgram = NormalizeProgramRequest(
                "legacy-program",
                request.ProgramName,
                request.LuaFileName,
                request.LuaContent);

            programs = [legacyProgram];
            targets = (request.RobotIds ?? [])
                .Where(robotId => robotId != Guid.Empty)
                .Distinct()
                .Select(robotId => new NormalizedFactoryRunTargetRequest(
                    robotId,
                    legacyProgram.Key))
                .ToList();
        }

        if (programs.Count > MaxProgramCount)
        {
            throw new InvalidOperationException(
                $"FactoryRun supports at most {MaxProgramCount} source programs.");
        }

        var duplicateProgramKey = programs
            .GroupBy(program => program.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateProgramKey is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate FactoryRun program key: {duplicateProgramKey.Key}.");
        }

        var totalLuaBytes = programs.Sum(program =>
            (long)Encoding.UTF8.GetByteCount(program.LuaContent));

        if (totalLuaBytes > MaxTotalLuaContentBytes)
        {
            throw new InvalidOperationException(
                "Total LuaContent payload must not exceed 5 MB.");
        }

        if (targets.Count == 0)
        {
            throw new InvalidOperationException("At least one robot target is required.");
        }

        if (targets.Count > MaxTargetCount)
        {
            throw new InvalidOperationException(
                $"FactoryRun supports at most {MaxTargetCount} robot targets.");
        }

        if (targets.Any(target => target.RobotId == Guid.Empty))
        {
            throw new InvalidOperationException("Every FactoryRun target requires RobotId.");
        }

        var duplicateRobot = targets
            .GroupBy(target => target.RobotId)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateRobot is not null)
        {
            throw new InvalidOperationException(
                $"Robot {duplicateRobot.Key} appears more than once in FactoryRun targets.");
        }

        var programKeys = programs
            .Select(program => program.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingProgramTarget = targets.FirstOrDefault(target =>
            string.IsNullOrWhiteSpace(target.ProgramKey) ||
            !programKeys.Contains(target.ProgramKey));

        if (missingProgramTarget is not null)
        {
            throw new InvalidOperationException(
                $"FactoryRun target {missingProgramTarget.RobotId} references unknown " +
                $"program key '{missingProgramTarget.ProgramKey}'.");
        }

        if (
            request.CoordinationMode == FactoryCoordinationMode.Synchronized &&
            targets
                .Select(target => programs.Single(program =>
                    string.Equals(
                        program.Key,
                        target.ProgramKey,
                        StringComparison.OrdinalIgnoreCase)))
                .Select(program => ComputeSha256(program.LuaContent))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Skip(1)
                .Any())
        {
            throw new InvalidOperationException(
                "Synchronized mode currently requires all targets to use the same Lua content. " +
                "Use ParallelIndependent for different Lua programs.");
        }

        return new NormalizedFactoryRunCreateRequest(programs, targets);
    }

    private static NormalizedFactoryRunProgramRequest NormalizeProgramRequest(
        string? key,
        string? programName,
        string? luaFileName,
        string? luaContent)
    {
        var normalizedKey = key?.Trim() ?? string.Empty;
        var normalizedProgramName = programName?.Trim() ?? string.Empty;
        var normalizedLuaFileName = luaFileName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedKey) || normalizedKey.Length > 100)
        {
            throw new InvalidOperationException(
                "Every FactoryRun program requires a key of at most 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(normalizedProgramName))
        {
            throw new InvalidOperationException("ProgramName is required.");
        }

        if (normalizedProgramName.Length > 100)
        {
            throw new InvalidOperationException(
                "ProgramName must not exceed 100 characters.");
        }

        if (
            string.IsNullOrWhiteSpace(normalizedLuaFileName) ||
            !normalizedLuaFileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Every FactoryRun program requires a .lua file name.");
        }

        if (normalizedLuaFileName.Length > 260)
        {
            throw new InvalidOperationException(
                "LuaFileName must not exceed 260 characters.");
        }

        if (string.IsNullOrWhiteSpace(luaContent))
        {
            throw new InvalidOperationException("LuaContent is required.");
        }

        if (Encoding.UTF8.GetByteCount(luaContent) > MaxLuaContentBytes)
        {
            throw new InvalidOperationException("LuaContent must not exceed 1 MB.");
        }

        return new NormalizedFactoryRunProgramRequest(
            normalizedKey,
            normalizedProgramName,
            normalizedLuaFileName,
            luaContent);
    }

    private async Task<bool> RefreshFromCommandsAsync(
        FactoryRun factoryRun,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var lockReleases = new List<(Guid RobotId, Guid OwnerId)>();

        foreach (var target in factoryRun.Targets)
        {
            var prepareCommand = target.PrepareCommand;

            if (prepareCommand is not null &&
                target.Status == FactoryRunTargetStatus.WaitingForDeviceReady &&
                prepareCommand.Status is CommandStatus.Completed or CommandStatus.Failed or CommandStatus.Timeout or CommandStatus.Cancelled)
            {
                if (prepareCommand.Status == CommandStatus.Completed)
                {
                    target.Status = FactoryRunTargetStatus.Ready;
                    target.ReadyAtUtc = prepareCommand.CompletedAt ?? DateTimeOffset.UtcNow;
                    target.ReadinessError = null;
                    target.FailureReason = null;
                }
                else
                {
                    target.Status = FactoryRunTargetStatus.Failed;
                    target.TerminationReason = prepareCommand.Status == CommandStatus.Timeout
                        ? FactoryRunTargetTerminationReason.Timeout
                        : FactoryRunTargetTerminationReason.CommandFailure;
                    target.ReadinessError = prepareCommand.FailureReason ?? prepareCommand.Status.ToString();
                    target.FailureReason = target.ReadinessError;
                    target.CompletedAtUtc = prepareCommand.CompletedAt ?? DateTimeOffset.UtcNow;
                    lockReleases.Add((target.RobotId, target.Id));
                }

                target.UpdatedAtUtc = DateTimeOffset.UtcNow;
                changed = true;
            }

            var runCommand = target.Command;

            if (runCommand is null)
            {
                continue;
            }

            if (target.Status is not (
    FactoryRunTargetStatus.Starting or
    FactoryRunTargetStatus.Armed or
    FactoryRunTargetStatus.Running))
            {
                continue;
            }

            if (runCommand.Status is CommandStatus.Completed or CommandStatus.Failed or CommandStatus.Timeout or CommandStatus.Cancelled)
            {
                target.Status = runCommand.Status switch
                {
                    CommandStatus.Completed => FactoryRunTargetStatus.Completed,
                    CommandStatus.Cancelled => FactoryRunTargetStatus.Cancelled,
                    _ => FactoryRunTargetStatus.Failed
                };
                target.TerminationReason = runCommand.Status switch
                {
                    CommandStatus.Completed => null,
                    CommandStatus.Timeout => FactoryRunTargetTerminationReason.Timeout,
                    CommandStatus.Cancelled when factoryRun.Status == FactoryRunStatus.Cancelling =>
                        FactoryRunTargetTerminationReason.UserCancelled,
                    CommandStatus.Cancelled => FactoryRunTargetTerminationReason.GroupPolicy,
                    _ when (runCommand.FailureReason ?? string.Empty).Contains(
                        "collision",
                        StringComparison.OrdinalIgnoreCase) =>
                        FactoryRunTargetTerminationReason.Collision,
                    _ => FactoryRunTargetTerminationReason.CommandFailure
                };

                target.CompletedAtUtc = runCommand.CompletedAt ?? DateTimeOffset.UtcNow;
                target.FailureReason = runCommand.Status == CommandStatus.Completed
                    ? null
                    : runCommand.FailureReason ?? runCommand.Status.ToString();
                target.UpdatedAtUtc = DateTimeOffset.UtcNow;
                lockReleases.Add((target.RobotId, runCommand.Id));
                changed = true;
            }
        }

        if (factoryRun.Status == FactoryRunStatus.WaitingForReady)
        {
            var readinessResolved = factoryRun.Targets.All(target => target.Status is
                FactoryRunTargetStatus.Ready or
                FactoryRunTargetStatus.Failed or
                FactoryRunTargetStatus.Cancelled);
            var readyTargetCount = factoryRun.Targets.Count(target =>
                target.Status == FactoryRunTargetStatus.Ready);
            var hasReadinessFailure = factoryRun.Targets.Any(target => target.Status is
                FactoryRunTargetStatus.Failed or FactoryRunTargetStatus.Cancelled);

            if (readinessResolved && readyTargetCount == 0)
            {
                factoryRun.Status = FactoryRunStatus.Failed;
                factoryRun.FailureReason = "No device became Ready.";
                factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;
                changed = true;
            }
            else if (
                readinessResolved &&
                hasReadinessFailure &&
                factoryRun.FailurePolicy == FactoryFailurePolicy.AbortExecutionGroup)
            {
                factoryRun.Status = FactoryRunStatus.Failed;
                factoryRun.FailureReason = "One or more devices failed to become Ready.";
                factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;
                changed = true;
            }
            else if (readinessResolved && readyTargetCount > 0)
            {
                factoryRun.Status = FactoryRunStatus.Ready;
                factoryRun.FailureReason = hasReadinessFailure
                    ? $"Factory run is ready with {readyTargetCount}/" +
                      $"{factoryRun.Targets.Count} surviving targets."
                    : null;
                factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;
                changed = true;
            }
        }

        if (factoryRun.Status is
            FactoryRunStatus.Starting or
            FactoryRunStatus.Running or
            FactoryRunStatus.RunningDegraded or
            FactoryRunStatus.Cancelling)
        {
            var hasFailedOrCancelledTarget = factoryRun.Targets.Any(target =>
                target.Status is FactoryRunTargetStatus.Failed or FactoryRunTargetStatus.Cancelled);
            var hasActiveTarget = factoryRun.Targets.Any(target => target.Status is
                FactoryRunTargetStatus.Starting or
                FactoryRunTargetStatus.Armed or
                FactoryRunTargetStatus.Running);

            if (
                factoryRun.FailurePolicy == FactoryFailurePolicy.IsolateTarget &&
                hasFailedOrCancelledTarget &&
                hasActiveTarget &&
                factoryRun.Status != FactoryRunStatus.Cancelling &&
                factoryRun.Status != FactoryRunStatus.RunningDegraded)
            {
                factoryRun.Status = FactoryRunStatus.RunningDegraded;
                factoryRun.FailureReason =
                    "One or more targets were isolated; surviving targets are still running.";
                factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;
                changed = true;
            }

            var allTerminal = factoryRun.Targets.All(target => target.Status is
                FactoryRunTargetStatus.Completed or
                FactoryRunTargetStatus.Failed or
                FactoryRunTargetStatus.Cancelled);

            if (allTerminal)
            {
                var now = DateTimeOffset.UtcNow;

                var completedTargetCount = factoryRun.Targets.Count(target =>
                    target.Status == FactoryRunTargetStatus.Completed);

                if (factoryRun.Status == FactoryRunStatus.Cancelling)
                {
                    factoryRun.Status = FactoryRunStatus.Cancelled;
                    factoryRun.CancelledAtUtc = now;
                    factoryRun.FailureReason = "Factory run was cancelled by the user.";
                }
                else if (completedTargetCount == factoryRun.Targets.Count)
                {
                    factoryRun.Status = FactoryRunStatus.Completed;
                    factoryRun.FailureReason = null;
                }
                else if (completedTargetCount > 0)
                {
                    factoryRun.Status = FactoryRunStatus.PartiallyCompleted;
                    factoryRun.FailureReason =
                        $"Factory run completed partially: {completedTargetCount}/" +
                        $"{factoryRun.Targets.Count} targets completed.";
                }
                else if (factoryRun.Targets.Any(target => target.Status == FactoryRunTargetStatus.Failed))
                {
                    factoryRun.Status = FactoryRunStatus.Failed;
                    factoryRun.FailureReason = "One or more factory run targets failed.";
                }
                else if (factoryRun.Targets.Any(target => target.Status == FactoryRunTargetStatus.Cancelled))
                {
                    factoryRun.Status = FactoryRunStatus.Cancelled;
                    factoryRun.CancelledAtUtc = now;
                    factoryRun.FailureReason = "Factory run was cancelled.";
                }
                factoryRun.CompletedAtUtc = now;
                factoryRun.UpdatedAtUtc = now;
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        foreach (var release in lockReleases.Distinct())
        {
            await _robotBusyLock.ReleaseAsync(
                release.RobotId,
                release.OwnerId,
                cancellationToken);
        }

        return true;
    }

    private void RecordFactoryRunOutcomeTransition(
        FactoryRunStatus previousStatus,
        FactoryRun factoryRun)
    {
        if (previousStatus == factoryRun.Status)
        {
            return;
        }

        var outcomeCode = factoryRun.Status switch
        {
            FactoryRunStatus.Completed => "completed",
            FactoryRunStatus.PartiallyCompleted => "partially_completed",
            FactoryRunStatus.Cancelled => "cancelled_by_user",
            FactoryRunStatus.Failed when previousStatus == FactoryRunStatus.WaitingForReady =>
                "readiness_failed",
            FactoryRunStatus.Failed => "target_failed",
            _ => null
        };

        if (outcomeCode is not null)
        {
            _metrics.RecordFactoryRunOutcome(
                outcomeCode,
                factoryRun.Targets.Count);
        }
    }

    private static RobotCommand CreateFactoryRunPrepareCommand(
    Guid userId,
    Guid robotId,
    Guid factoryRunId,
    Guid targetId,
    Guid programId,
    string programName)
    {
        var now = DateTimeOffset.UtcNow;

        return new RobotCommand
        {
            Id = Guid.NewGuid(),
            RobotId = robotId,
            UserId = userId,
            CommandType = RobotCommandType.PrepareProgram,
            PayloadJson = JsonSerializer.Serialize(new
            {
                factoryRunId,
                targetId,
                programId,
                programName,
                requestedAtUtc = now
            }),
            Status = CommandStatus.Pending,
            CreatedAt = now,
            TimeoutAt = now.Add(DefaultCommandTimeout)
        };
    }

    private async Task<int> DispatchIndependentCommandsAsync(
        FactoryRun factoryRun,
        IReadOnlyCollection<RobotCommand> commands,
        CancellationToken cancellationToken)
    {
        var dispatchedCount = 0;
        var failedDispatches = new List<(RobotCommand Command, FactoryRunTarget Target)>();

        foreach (var command in commands)
        {
            var target = factoryRun.Targets.First(item => item.CommandId == command.Id);
            var timeoutScheduled = false;

            try
            {
                await _commandTimeoutScheduler.ScheduleAsync(
                    command,
                    cancellationToken);
                timeoutScheduled = true;

                await _commandQueue.EnqueueAsync(command, cancellationToken);
                dispatchedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failedAt = DateTimeOffset.UtcNow;
                var failureReason =
                    $"Independent command dispatch failed: {exception.Message}";

                command.Status = CommandStatus.Failed;
                command.CompletedAt = failedAt;
                command.TimeoutAt = null;
                command.FailureReason = failureReason;

                target.Status = FactoryRunTargetStatus.Failed;
                target.TerminationReason =
                    FactoryRunTargetTerminationReason.CommandFailure;
                target.CompletedAtUtc = failedAt;
                target.FailureReason = failureReason;
                target.UpdatedAtUtc = failedAt;

                failedDispatches.Add((command, target));

                if (timeoutScheduled)
                {
                    try
                    {
                        await _commandTimeoutScheduler.RemoveAsync(
                            command.Id,
                            CancellationToken.None);
                    }
                    catch
                    {
                        // The command is terminal in SQL. The timeout worker can
                        // safely discard a stale Redis entry if cleanup is delayed.
                    }
                }
            }
        }

        if (failedDispatches.Count == 0)
        {
            return dispatchedCount;
        }

        factoryRun.FailureReason =
            $"Factory run started with {dispatchedCount}/{commands.Count} " +
            "independent commands dispatched.";
        factoryRun.UpdatedAtUtc = DateTimeOffset.UtcNow;

        // Persist terminal targets before releasing their owner-aware locks.
        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        foreach (var failedDispatch in failedDispatches)
        {
            await _robotBusyLock.ReleaseAsync(
                failedDispatch.Target.RobotId,
                failedDispatch.Command.Id,
                CancellationToken.None);
        }

        return dispatchedCount;
    }

    private static string BuildFactoryRunSnapshotPayload(
        FactoryRunTarget target,
        Guid factoryRunId,
        FactoryCoordinationMode coordinationMode,
        FactoryFailurePolicy failurePolicy,
        DateTimeOffset? scheduledStartAtUtc)
    {
        var program = target.Program;

        if (program is null)
        {
            throw new InvalidOperationException(
                "Robot program was not loaded for the factory run target.");
        }

        if (program.Status != RobotProgramStatus.Published)
        {
            throw new InvalidOperationException(
                "Only published robot programs can be run.");
        }

        if (program.Steps.Count == 0)
        {
            throw new InvalidOperationException(
                "Robot program must have at least one step.");
        }

        var snapshot = new
        {
            programId = program.Id,
            factoryRunId,
            targetId = target.Id,
            coordinationMode = coordinationMode.ToString(),
            syncMode = coordinationMode == FactoryCoordinationMode.Synchronized
                ? "Barrier"
                : "Independent",
            failurePolicy = failurePolicy.ToString(),
            programName = program.Name,
            programStatus = program.Status.ToString(),
            snapshottedAt = DateTimeOffset.UtcNow,
            scheduledStartAtUtc,
            steps = program.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(step => new
                {
                    id = step.Id,
                    orderIndex = step.OrderIndex,
                    stepType = step.StepType.ToString(),
                    label = step.Label,
                    payload = string.IsNullOrWhiteSpace(step.PayloadJson)
                        ? JsonSerializer.Deserialize<JsonElement>("{}")
                        : JsonSerializer.Deserialize<JsonElement>(step.PayloadJson)
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot);
    }

    private static CreateRobotProgramRequest CloneCreateProgramRequest(
    CreateRobotProgramRequest request)
    {
        return new CreateRobotProgramRequest
        {
            Name = request.Name,
            Status = request.Status,
            Source = request.Source,
            Steps = request.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(step => new RobotProgramStepRequest
                {
                    OrderIndex = step.OrderIndex,
                    StepType = step.StepType,
                    Label = step.Label,
                    Payload = JsonSerializer.Deserialize<JsonElement>(step.Payload.GetRawText())
                })
                .ToList()
        };
    }

    private static IReadOnlyList<int> DeserializeDurations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private static string SerializeDurations(IReadOnlyList<int> durations)
    {
        return JsonSerializer.Serialize(durations.Select(duration => Math.Max(0, duration)).ToArray());
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static FactoryRunResponse ToResponse(FactoryRun factoryRun)
    {
        return new FactoryRunResponse
        {
            Id = factoryRun.Id,
            CompanyId = factoryRun.CompanyId,
            CreatedByUserId = factoryRun.CreatedByUserId,
            Status = factoryRun.Status.ToString(),
            CoordinationMode = factoryRun.CoordinationMode.ToString(),
            FailurePolicy = factoryRun.FailurePolicy.ToString(),
            ProgramName = factoryRun.ProgramName,
            LuaFileName = factoryRun.LuaFileName,
            LuaContentHash = factoryRun.LuaContentHash,
            TargetCount = factoryRun.TargetCount,
            ScheduledStartAtUtc = factoryRun.ScheduledStartAtUtc,
            StepDurationsMs = DeserializeDurations(factoryRun.StepDurationsJson),
            PreparedAtUtc = factoryRun.PreparedAtUtc,
            StartedAtUtc = factoryRun.StartedAtUtc,
            ActualStartSkewMs = factoryRun.ActualStartSkewMs,
            CompletedAtUtc = factoryRun.CompletedAtUtc,
            CancelledAtUtc = factoryRun.CancelledAtUtc,
            FailureReason = factoryRun.FailureReason,
            CreatedAtUtc = factoryRun.CreatedAtUtc,
            UpdatedAtUtc = factoryRun.UpdatedAtUtc,
            Programs = factoryRun.Programs
                .OrderBy(program => program.CreatedAtUtc)
                .Select(program => new FactoryRunProgramResponse
                {
                    Id = program.Id,
                    FactoryRunId = factoryRun.Id,
                    ProgramKey = program.ProgramKey,
                    ProgramName = program.ProgramName,
                    LuaFileName = program.LuaFileName,
                    LuaContentHash = program.LuaContentHash,
                    SyncPlanHash = program.SyncPlanHash
                })
                .ToList(),
            Targets = factoryRun.Targets
                .OrderBy(target => target.CreatedAtUtc)
                .Select(ToTargetResponse)
                .ToList()
        };
    }

    private static FactoryRunTargetResponse ToTargetResponse(FactoryRunTarget target)
    {
        return new FactoryRunTargetResponse
        {
            Id = target.Id,
            FactoryRunId = target.FactoryRunId,
            RobotId = target.RobotId,
            FactoryRunProgramId = target.FactoryRunProgramId,
            ProgramId = target.ProgramId,
            PrepareCommandId = target.PrepareCommandId,
            CommandId = target.CommandId,

            RuntimeSessionId = target.RuntimeSessionId,
            Status = target.Status.ToString(),
            TerminationReason = target.TerminationReason?.ToString(),
            ReadinessError = target.ReadinessError,
            PrepareStartedAtUtc = target.PrepareStartedAtUtc,
            PreparedAtUtc = target.PreparedAtUtc,
            ReadyAtUtc = target.ReadyAtUtc,
            CommandReceivedAtUtc = target.CommandReceivedAtUtc,
            ArmedAtUtc = target.ArmedAtUtc,
            EstimatedStepDurationsMs = DeserializeDurations(target.EstimatedStepDurationsJson),
            StartedAtUtc = target.StartedAtUtc,
            ActualStartedAtUtc = target.ActualStartedAtUtc,
            StartLateByMs = target.StartLateByMs,
            CompletedAtUtc = target.CompletedAtUtc,
            FailureReason = target.FailureReason
        };
    }

    private sealed record NormalizedFactoryRunCreateRequest(
        IReadOnlyList<NormalizedFactoryRunProgramRequest> Programs,
        IReadOnlyList<NormalizedFactoryRunTargetRequest> Targets);

    private sealed record NormalizedFactoryRunProgramRequest(
        string Key,
        string ProgramName,
        string LuaFileName,
        string LuaContent);

    private sealed record NormalizedFactoryRunTargetRequest(
        Guid RobotId,
        string ProgramKey);
}
