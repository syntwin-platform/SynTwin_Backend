using System.Text.Json;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Dtos;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.RobotPrograms.Interfaces;


namespace Syntwin.Application.Commands.Services;

public sealed class RobotCommandService : IRobotCommandService
{
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromMinutes(5);

    private readonly IRobotRepository _robotRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRobotProgramRepository _programRepository;

    public RobotCommandService(
     IRobotRepository robotRepository,
     IUserRepository userRepository,
     IRobotCommandRepository commandRepository,
     IAuditLogRepository auditLogRepository,
     IRobotProgramRepository programRepository)
    {
        _robotRepository = robotRepository;
        _userRepository = userRepository;
        _commandRepository = commandRepository;
        _auditLogRepository = auditLogRepository;
        _programRepository = programRepository;
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

        if (user is null || robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (user.Role == UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException("Super Admin cannot control customer robots.");
        }

        var plan = user.Subscriptions
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
                Message = "Command was blocked because current plan cannot send commands.",
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            await _commandRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Premium plan is required to send robot commands.");
        }

        if (!Enum.TryParse<RobotCommandType>(request.CommandType, true, out var commandType))
        {
            throw new InvalidOperationException("Invalid command type.");
        }

        ValidateCommandPayload(commandType, request.Payload);

        var payloadJson = commandType == RobotCommandType.RunProgram
             ? await BuildRunProgramSnapshotPayloadAsync(
         robotId,
         request.Payload,
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

        await _commandRepository.AddAsync(command, cancellationToken);
        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robotId,
            Action = "COMMAND_REQUESTED",
            IpAddress = ipAddress,
            RawPayloadJson = command.PayloadJson,
            Message = $"Command {command.CommandType} was requested.",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _commandRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(command);
    }

    public async Task<IReadOnlyList<RobotCommandResponse>?> ListAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        var commands = await _commandRepository.ListByRobotIdAsync(robotId, cancellationToken);
        return commands.Select(ToResponse).ToList();
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
    Guid robotId,
    JsonElement? payload,
    CancellationToken cancellationToken)
    {
        var programId = ReadProgramId(payload);

        var program = await _programRepository.GetByIdForRobotAsync(
            robotId,
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
                    step.Id,
                    step.OrderIndex,
                    stepType = step.StepType.ToString(),
                    step.Label,
                    payload = string.IsNullOrWhiteSpace(step.PayloadJson)
                        ? JsonSerializer.Deserialize<JsonElement>("{}")
                        : JsonSerializer.Deserialize<JsonElement>(step.PayloadJson)
                })
                .ToList()
        };

        return JsonSerializer.Serialize(snapshot);
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
            CreatedAt = command.CreatedAt
        };
    }
}
