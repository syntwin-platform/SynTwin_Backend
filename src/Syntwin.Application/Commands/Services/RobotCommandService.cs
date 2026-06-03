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

        var payloadJson = commandType == RobotCommandType.RunProgram
     ? await BuildRunProgramSnapshotPayloadAsync(
         robotId,
         request.Payload,
         cancellationToken)
     : request.Payload?.GetRawText();

        var command = new RobotCommand
        {
            Id = Guid.NewGuid(),
            RobotId = robotId,
            UserId = userId,
            CommandType = commandType,
            PayloadJson = payloadJson,
            Status = CommandStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
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