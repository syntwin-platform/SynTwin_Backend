using System.Text.Json;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Dtos;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.Companies.Interfaces;
using Microsoft.Extensions.Options;
using Syntwin.Application.Robots.Options;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Exceptions;

namespace Syntwin.Application.Commands.Services;

public sealed class RobotCommandService : IRobotCommandService
{
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromMinutes(5);

    private readonly IRobotRepository _robotRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IRobotCommandQueue _commandQueue;
    private readonly IRobotCommandTimeoutScheduler _commandTimeoutScheduler;
    private readonly IRobotBusyLock _robotBusyLock;
    private readonly TimeSpan _robotBusyLockTtl;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRobotProgramRepository _programRepository;
    private readonly IRobotAccessService _robotAccessService;
    private readonly ICompanyRepository _companyRepository;
    private readonly IRobotSafetyValidationService _safetyValidationService;

    public RobotCommandService(
        IRobotRepository robotRepository,
        IUserRepository userRepository,
        IRobotCommandRepository commandRepository,
        IRobotCommandQueue commandQueue,
        IRobotCommandTimeoutScheduler commandTimeoutScheduler,
        IAuditLogRepository auditLogRepository,
        IRobotProgramRepository programRepository,
        IRobotAccessService robotAccessService,
        ICompanyRepository companyRepository,
        IRobotBusyLock robotBusyLock,
        IOptions<RobotRuntimeOptions> options,
        IRobotSafetyValidationService safetyValidationService)
    {
        _robotRepository = robotRepository;
        _userRepository = userRepository;
        _commandRepository = commandRepository;
        _commandQueue = commandQueue;
        _commandTimeoutScheduler = commandTimeoutScheduler;
        _auditLogRepository = auditLogRepository;
        _programRepository = programRepository;
        _robotAccessService = robotAccessService;
        _companyRepository = companyRepository;
        _robotBusyLock = robotBusyLock;
        _robotBusyLockTtl = TimeSpan.FromSeconds(
            Math.Max(30, options.Value.RobotBusyLockTtlSeconds));
        _safetyValidationService = safetyValidationService;
    }

    public async Task<RobotCommandResponse?> CreateAsync(
        Guid userId,
        Guid robotId,
        CreateRobotCommandRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (user is null || robot is null)
        {
            return null;
        }

        var role = await _robotAccessService.GetCompanyRoleAsync(
            userId,
            robot.CompanyId,
            cancellationToken);

        if (!role.HasValue)
        {
            return null;
        }

        if (user.Role == UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Super Admin cannot control customer robots.");
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot receive commands.");
        }

        var company = await _companyRepository.GetByIdAsync(
     robot.CompanyId,
     cancellationToken);

        if (company is null || company.Status != CompanyStatus.Active)
        {
            return null;
        }

        var companyOwner = await _userRepository.GetByIdAsync(
            company.CreatedByUserId,
            cancellationToken);

        if (companyOwner is null)
        {
            throw new InvalidOperationException("Company owner not found.");
        }

        var plan = companyOwner.Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan)
            .FirstOrDefault();

        if (plan?.CanSendCommand != true)
        {
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = userId,
                RobotId = robotId,
                Action = "COMMAND_BLOCKED_REQUIRE_PREMIUM",
                IpAddress = ipAddress,
                Message = "Command was blocked because company owner's plan cannot send commands.",
                RawPayloadJson = CreateAuditContext(
        robot.CompanyId,
        role.Value,
        request.CommandType,
        request.Payload?.GetRawText()),
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await _commandRepository.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedAccessException(
                "Current subscription plan cannot send robot commands.");
        }

        if (!Enum.TryParse<RobotCommandType>(request.CommandType, true, out var commandType))
        {
            throw new InvalidOperationException("Invalid command type.");
        }

        ValidateCommandPayload(commandType, request.Payload);

        var payloadJson = commandType == RobotCommandType.RunProgram
            ? await BuildRunProgramSnapshotPayloadAsync(
                userId,
                robot,
                role.Value,
                request.Payload,
                ipAddress,
                cancellationToken)
            : request.Payload?.GetRawText();

        var now = DateTimeOffset.UtcNow;

        var command = new RobotCommand
        {
            Id = Guid.NewGuid(),
            RobotId = robotId,
            UserId = userId,
            CommandType = commandType,
            PayloadJson = payloadJson,
            Status = CommandStatus.Pending,
            CreatedAt = now,
            TimeoutAt = now.Add(DefaultCommandTimeout)
        };

        var busyLockAcquired = false;

        if (IsBusyLockCommand(command.CommandType))
        {
            busyLockAcquired = await _robotBusyLock.TryAcquireAsync(
                robotId,
                command.Id,
                _robotBusyLockTtl,
                cancellationToken);

            if (!busyLockAcquired)
            {
                throw new InvalidOperationException("Robot already has an active command.");
            }
        }

        try
        {
            await _commandRepository.AddAsync(command, cancellationToken);
            await _auditLogRepository.AddAsync(new AuditLog
            {
                UserId = userId,
                RobotId = robotId,
                Action = "COMMAND_REQUESTED",
                IpAddress = ipAddress,
                RawPayloadJson = CreateAuditContext(
        robot.CompanyId,
        role.Value,
        command.CommandType.ToString(),
        command.PayloadJson),
                Message = $"Command {command.CommandType} was requested.",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await _commandRepository.SaveChangesAsync(cancellationToken);
            await _commandTimeoutScheduler.ScheduleAsync(command, cancellationToken);
            await _commandQueue.EnqueueAsync(command, cancellationToken);

            return ToResponse(command);
        }
        catch
        {
            if (busyLockAcquired)
            {
                await _robotBusyLock.ReleaseAsync(
                    robotId,
                    command.Id,
                    cancellationToken);
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<RobotCommandResponse>?> ListAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null)
        {
            return null;
        }

        var role = await _robotAccessService.GetCompanyRoleAsync(
            userId,
            robot.CompanyId,
            cancellationToken);

        if (!role.HasValue)
        {
            return null;
        }

        var commands = await _commandRepository.ListByRobotIdAsync(robotId, cancellationToken);
        return commands.Select(ToResponse).ToList();
    }

    private static string CreateAuditContext(
    Guid companyId,
    CompanyMemberRole actorCompanyRole,
    string commandType,
    string? payloadJson)
    {
        JsonElement? payload = null;

        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        }

        return JsonSerializer.Serialize(new
        {
            companyId,
            actorCompanyRole = actorCompanyRole.ToString(),
            commandType,
            payload
        });
    }
    private static void ValidateCommandPayload(
    RobotCommandType commandType,
    JsonElement? payload)
    {
        switch (commandType)
        {
            case RobotCommandType.MoveJ:
                ValidateMoveJPayload(payload);
                break;

            case RobotCommandType.MoveL:
                ValidateMoveLPayload(payload);
                break;

            case RobotCommandType.SetDO:
                ValidateSetDoPayload(payload);
                break;

            default:
                break;
        }
    }

    private static void ValidateMoveJPayload(JsonElement? payload)
    {
        var payloadObject = RequirePayloadObject(payload, RobotCommandType.MoveJ.ToString());

        var jointAngles = RequireArray(
            payloadObject,
            "jointAngles",
            RobotCommandType.MoveJ.ToString());

        if (jointAngles.GetArrayLength() != 6)
        {
            throw new InvalidOperationException("MoveJ payload jointAngles must contain exactly 6 values.");
        }

        foreach (var jointAngle in jointAngles.EnumerateArray())
        {
            if (jointAngle.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException("MoveJ payload jointAngles must contain only numbers.");
            }
        }

        ValidatePercentIfPresent(payloadObject, "speed", RobotCommandType.MoveJ.ToString());
        ValidatePercentIfPresent(payloadObject, "acc", RobotCommandType.MoveJ.ToString());
    }

    private static void ValidateMoveLPayload(JsonElement? payload)
    {
        var payloadObject = RequirePayloadObject(payload, RobotCommandType.MoveL.ToString());

        if (!payloadObject.TryGetProperty("tcpPose", out var tcpPose) ||
            tcpPose.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("MoveL payload must include tcpPose object.");
        }

        RequireNumber(tcpPose, "x", "MoveL.tcpPose");
        RequireNumber(tcpPose, "y", "MoveL.tcpPose");
        RequireNumber(tcpPose, "z", "MoveL.tcpPose");
        RequireNumber(tcpPose, "rx", "MoveL.tcpPose");
        RequireNumber(tcpPose, "ry", "MoveL.tcpPose");
        RequireNumber(tcpPose, "rz", "MoveL.tcpPose");

        ValidatePercentIfPresent(payloadObject, "speed", RobotCommandType.MoveL.ToString());
        ValidatePercentIfPresent(payloadObject, "acc", RobotCommandType.MoveL.ToString());
    }

    private static void ValidateSetDoPayload(JsonElement? payload)
    {
        var payloadObject = RequirePayloadObject(payload, RobotCommandType.SetDO.ToString());

        var doType = RequireString(payloadObject, "doType", RobotCommandType.SetDO.ToString())
            .Trim()
            .ToLowerInvariant();

        if (doType is not "cabinet" and not "tool")
        {
            throw new InvalidOperationException("SetDO payload doType must be cabinet or tool.");
        }

        var doIndex = RequireInt(payloadObject, "doIndex", RobotCommandType.SetDO.ToString());
        var doValue = RequireInt(payloadObject, "doValue", RobotCommandType.SetDO.ToString());

        if (doType == "cabinet" && doIndex is < 1 or > 8)
        {
            throw new InvalidOperationException("SetDO cabinet doIndex must be between 1 and 8.");
        }

        if (doType == "tool" && doIndex is < 0 or > 1)
        {
            throw new InvalidOperationException("SetDO tool doIndex must be between 0 and 1.");
        }

        if (doValue is not 0 and not 1)
        {
            throw new InvalidOperationException("SetDO doValue must be 0 or 1.");
        }
    }

    private static JsonElement RequirePayloadObject(
        JsonElement? payload,
        string commandType)
    {
        if (payload is null ||
            payload.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException($"{commandType} payload is required.");
        }

        if (payload.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{commandType} payload must be an object.");
        }

        return payload.Value;
    }

    private static JsonElement RequireArray(
        JsonElement payload,
        string propertyName,
        string scope)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{scope} payload must include {propertyName} array.");
        }

        return value;
    }

    private static double RequireNumber(
        JsonElement payload,
        string propertyName,
        string scope)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{scope} must include numeric {propertyName}.");
        }

        return value.GetDouble();
    }

    private static int RequireInt(
        JsonElement payload,
        string propertyName,
        string scope)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var intValue))
        {
            throw new InvalidOperationException($"{scope} payload must include integer {propertyName}.");
        }

        return intValue;
    }

    private static string RequireString(
        JsonElement payload,
        string propertyName,
        string scope)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"{scope} payload must include string {propertyName}.");
        }

        return value.GetString()!;
    }

    private static void ValidatePercentIfPresent(
        JsonElement payload,
        string propertyName,
        string scope)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{scope} payload {propertyName} must be a number.");
        }

        var number = value.GetDouble();

        if (number is < 1 or > 100)
        {
            throw new InvalidOperationException($"{scope} payload {propertyName} must be between 1 and 100.");
        }
    }

    private async Task<string> BuildRunProgramSnapshotPayloadAsync(
        Guid userId,
        Robot robot,
        CompanyMemberRole actorCompanyRole,
        JsonElement? payload,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var programId = ReadProgramId(payload);

        var program = await _programRepository.GetByIdForRobotAsync(
            robot.Id,
            programId,
            cancellationToken);

        if (program is null)
        {
            throw new InvalidOperationException("Robot program not found.");
        }

        if (program.Status != RobotProgramStatus.Published)
        {
            throw new InvalidOperationException("Only published robot programs can be run.");
        }

        if (program.Steps.Count == 0)
        {
            throw new InvalidOperationException("Robot program must have at least one step.");
        }

        var safetyResult = await _safetyValidationService.ValidateProgramAsync(
            CreateSafetyValidationRequest(robot, program),
            cancellationToken);

        if (safetyResult.HasBlockers)
        {
            await AddCommandSafetyBlockedAuditAsync(
                userId,
                robot.CompanyId,
                actorCompanyRole,
                program,
                safetyResult,
                ipAddress,
                cancellationToken);

            await _commandRepository.SaveChangesAsync(cancellationToken);

            throw new RobotSafetyValidationException(safetyResult);
        }

        var snapshot = new
        {
            programId = program.Id,
            programName = program.Name,
            programStatus = program.Status.ToString(),
            snapshottedAt = DateTimeOffset.UtcNow,
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

    private static RobotSafetyValidationRequest CreateSafetyValidationRequest(
        Robot robot,
        RobotProgram program)
    {
        return new RobotSafetyValidationRequest
        {
            RobotId = robot.Id,
            CompanyId = robot.CompanyId,
            RobotModel = robot.Model,
            CurrentJointAngles = null,
            Steps = program.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(step => new RobotProgramStepSafetyInput
                {
                    OrderIndex = step.OrderIndex,
                    StepType = step.StepType.ToString(),
                    Label = step.Label,
                    Payload = string.IsNullOrWhiteSpace(step.PayloadJson)
                        ? JsonSerializer.Deserialize<JsonElement>("{}")
                        : JsonSerializer.Deserialize<JsonElement>(step.PayloadJson)
                })
                .ToList()
        };
    }

    private async Task AddCommandSafetyBlockedAuditAsync(
        Guid userId,
        Guid companyId,
        CompanyMemberRole actorCompanyRole,
        RobotProgram program,
        SafetyValidationResult safetyResult,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            companyId,
            actorCompanyRole = actorCompanyRole.ToString(),
            programId = program.Id,
            programName = program.Name,
            diagnostics = safetyResult.Diagnostics
        });

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = program.RobotId,
            Action = "COMMAND_BLOCKED_SAFETY",
            IpAddress = ipAddress,
            Message = $"RunProgram for program '{program.Name}' was blocked by safety validation.",
            RawPayloadJson = payload,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static Guid ReadProgramId(JsonElement? payload)
    {
        if (payload is null ||
            payload.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException("RunProgram payload must include programId.");
        }

        if (!payload.Value.TryGetProperty("programId", out var programIdElement))
        {
            throw new InvalidOperationException("RunProgram payload must include programId.");
        }

        if (programIdElement.ValueKind == JsonValueKind.String &&
            Guid.TryParse(programIdElement.GetString(), out var programId))
        {
            return programId;
        }

        throw new InvalidOperationException("RunProgram programId must be a valid GUID.");
    }

    private static bool IsBusyLockCommand(RobotCommandType commandType)
    {
        return commandType != RobotCommandType.EStop;
    }

    private static RobotCommandResponse ToResponse(RobotCommand command)
    {
        return new RobotCommandResponse
        {
            Id = command.Id,
            RobotId = command.RobotId,
            CommandType = command.CommandType.ToString(),
            Payload = string.IsNullOrWhiteSpace(command.PayloadJson)
                ? null
                : JsonSerializer.Deserialize<JsonElement>(command.PayloadJson),
            Status = command.Status.ToString(),
            CreatedAt = command.CreatedAt,
            CompletedAt = command.CompletedAt,
            FailureReason = command.FailureReason,
            Result = command.Result is null
                ? null
                : new RobotCommandResultResponse
                {
                    Success = command.Result.Success,
                    Message = command.Result.Message,
                    RawPayload = string.IsNullOrWhiteSpace(command.Result.RawPayloadJson)
                        ? null
                        : JsonSerializer.Deserialize<JsonElement>(command.Result.RawPayloadJson),
                    CompletedAt = command.Result.CompletedAt
                }
        };
    }

}
