using System.Text.Json;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;


namespace Syntwin.Application.RobotPrograms.Services;

public sealed class RobotProgramService : IRobotProgramService
{
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotProgramRepository _programRepository;
    private readonly IRobotRealtimeNotifier _realtimeNotifier;

    public RobotProgramService(
      IRobotRepository robotRepository,
      IRobotProgramRepository programRepository,
      IRobotRealtimeNotifier realtimeNotifier)
    {
        _robotRepository = robotRepository;
        _programRepository = programRepository;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<IReadOnlyList<RobotProgramResponse>?> ListAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        var programs = await _programRepository.ListByRobotIdAsync(robotId, cancellationToken);

        return programs.Select(ToResponse).ToList();
    }

    public async Task<RobotProgramResponse?> GetByIdAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        var program = await _programRepository.GetByIdForRobotAsync(
            robotId,
            programId,
            cancellationToken);

        return program is null ? null : ToResponse(program);
    }

    public async Task<RobotProgramResponse?> CreateAsync(
        Guid userId,
        Guid robotId,
        CreateRobotProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot create programs.");
        }

        ValidateProgramName(request.Name);

        var status = ParseStatusForCreate(request.Status);
        var source = ParseSourceOrDefault(request.Source, RobotProgramSource.Studio);
        var now = DateTimeOffset.UtcNow;

        ValidateSteps(request.Steps);

        var program = new RobotProgram
        {
            Id = Guid.NewGuid(),
            RobotId = robotId,
            Name = request.Name.Trim(),
            Status = status,
            Source = source,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,
            Steps = request.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(step => ToEntity(step, now))
                .ToList()
        };

        await _programRepository.AddAsync(program, cancellationToken);
        await _programRepository.SaveChangesAsync(cancellationToken);
        await _realtimeNotifier.NotifyProgramUpdatedAsync(ToProgramUpdatedEvent(program, "Created"), cancellationToken);
        return ToResponse(program);
    }

    public async Task<RobotProgramResponse?> UpdateAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        UpdateRobotProgramRequest request,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot update programs.");
        }

        var program = await _programRepository.GetByIdForRobotAsync(
            robotId,
            programId,
            cancellationToken);

        if (program is null)
        {
            return null;
        }

        if (program.Status == RobotProgramStatus.Archived)
        {
            throw new InvalidOperationException("Archived program cannot be updated.");
        }

        ValidateSteps(request.Steps);

        var now = DateTimeOffset.UtcNow;

        ValidateProgramName(request.Name);

        program.Name = request.Name.Trim();
        program.Status = ParseStatusForUpdate(request.Status, program.Status);
        program.UpdatedAt = now;

        ApplyStepUpdates(program, request.Steps, now);

        await _programRepository.SaveChangesAsync(cancellationToken);
        await _realtimeNotifier.NotifyProgramUpdatedAsync(
    ToProgramUpdatedEvent(program, "Updated"),
    cancellationToken);

        return ToResponse(program);
    }

    public async Task<RobotProgramResponse?> PublishAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot publish programs.");
        }

        var program = await _programRepository.GetByIdForRobotAsync(
            robotId,
            programId,
            cancellationToken);

        if (program is null)
        {
            return null;
        }

        if (program.Status == RobotProgramStatus.Archived)
        {
            throw new InvalidOperationException("Archived program cannot be published.");
        }

        if (program.Steps.Count == 0)
        {
            throw new InvalidOperationException("Program must have at least one step before publishing.");
        }

        program.Status = RobotProgramStatus.Published;
        program.UpdatedAt = DateTimeOffset.UtcNow;

        await _programRepository.SaveChangesAsync(cancellationToken);
        await _realtimeNotifier.NotifyProgramUpdatedAsync(
    ToProgramUpdatedEvent(program, "Published"),
    cancellationToken);
        return ToResponse(program);
    }

    public async Task<bool> ArchiveAsync(
        Guid userId,
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return false;
        }

        var program = await _programRepository.GetByIdForRobotAsync(
            robotId,
            programId,
            cancellationToken);

        if (program is null)
        {
            return false;
        }

        if (program.Status == RobotProgramStatus.Archived)
        {
            return true;
        }

        program.Status = RobotProgramStatus.Archived;
        program.UpdatedAt = DateTimeOffset.UtcNow;

        await _programRepository.SaveChangesAsync(cancellationToken);
        await _realtimeNotifier.NotifyProgramUpdatedAsync(
    ToProgramUpdatedEvent(program, "Archived"),
    cancellationToken);
        return true;
    }

    private static void ValidateSteps(IReadOnlyList<RobotProgramStepRequest> steps)
    {
        if (steps.Count == 0)
        {
            throw new InvalidOperationException("Program must have at least one step.");
        }

        var duplicateOrderIndex = steps
            .GroupBy(step => step.OrderIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateOrderIndex is not null)
        {
            throw new InvalidOperationException($"Duplicate step order index: {duplicateOrderIndex.Key}.");
        }

        foreach (var step in steps)
        {
            if (!Enum.TryParse<RobotProgramStepType>(step.StepType, true, out var stepType))
            {
                throw new InvalidOperationException($"Invalid step type: {step.StepType}.");
            }

            if (string.IsNullOrWhiteSpace(step.Label))
            {
                throw new InvalidOperationException($"Step label is required at order index {step.OrderIndex}.");
            }

            if (step.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException($"Step payload is required at order index {step.OrderIndex}.");
            }

            ValidateStepPayload(stepType, step.Payload, step.OrderIndex);
        }
    }

    private static void ValidateStepPayload(
    RobotProgramStepType stepType,
    JsonElement payload,
    int orderIndex)
    {
        switch (stepType)
        {
            case RobotProgramStepType.MoveJ:
                ValidateMoveJStepPayload(payload, orderIndex);
                break;

            case RobotProgramStepType.MoveL:
            case RobotProgramStepType.MoveTCP:
                ValidateTcpPoseStepPayload(payload, stepType.ToString(), orderIndex);
                break;

            case RobotProgramStepType.RotateJoint:
                ValidateRotateJointStepPayload(payload, orderIndex);
                break;

            case RobotProgramStepType.SetDO:
                ValidateSetDoStepPayload(payload, orderIndex);
                break;

            case RobotProgramStepType.WaitMs:
                ValidateWaitMsStepPayload(payload, orderIndex);
                break;

            case RobotProgramStepType.GripperOpen:
            case RobotProgramStepType.GripperClose:
            case RobotProgramStepType.Comment:
                RequirePayloadObject(payload, $"{stepType} step at order index {orderIndex}");
                break;

            default:
                throw new InvalidOperationException($"Unsupported step type: {stepType}.");
        }
    }

    private static void ValidateMoveJStepPayload(JsonElement payload, int orderIndex)
    {
        var payloadObject = RequirePayloadObject(payload, $"MoveJ step at order index {orderIndex}");

        var jointAngles = RequireArray(payloadObject, "jointAngles", $"MoveJ step at order index {orderIndex}");

        if (jointAngles.GetArrayLength() != 6)
        {
            throw new InvalidOperationException($"MoveJ step at order index {orderIndex} jointAngles must contain exactly 6 values.");
        }

        foreach (var jointAngle in jointAngles.EnumerateArray())
        {
            if (jointAngle.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"MoveJ step at order index {orderIndex} jointAngles must contain only numbers.");
            }
        }

        ValidatePercentIfPresent(payloadObject, "speed", $"MoveJ step at order index {orderIndex}");
        ValidatePercentIfPresent(payloadObject, "acc", $"MoveJ step at order index {orderIndex}");
    }

    private static void ValidateTcpPoseStepPayload(
        JsonElement payload,
        string stepType,
        int orderIndex)
    {
        var payloadObject = RequirePayloadObject(payload, $"{stepType} step at order index {orderIndex}");

        if (!payloadObject.TryGetProperty("tcpPose", out var tcpPose) ||
            tcpPose.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{stepType} step at order index {orderIndex} must include tcpPose object.");
        }

        RequireNumber(tcpPose, "x", $"{stepType}.tcpPose step at order index {orderIndex}");
        RequireNumber(tcpPose, "y", $"{stepType}.tcpPose step at order index {orderIndex}");
        RequireNumber(tcpPose, "z", $"{stepType}.tcpPose step at order index {orderIndex}");
        RequireNumber(tcpPose, "rx", $"{stepType}.tcpPose step at order index {orderIndex}");
        RequireNumber(tcpPose, "ry", $"{stepType}.tcpPose step at order index {orderIndex}");
        RequireNumber(tcpPose, "rz", $"{stepType}.tcpPose step at order index {orderIndex}");

        ValidatePercentIfPresent(payloadObject, "speed", $"{stepType} step at order index {orderIndex}");
        ValidatePercentIfPresent(payloadObject, "acc", $"{stepType} step at order index {orderIndex}");
    }

    private static void ValidateRotateJointStepPayload(JsonElement payload, int orderIndex)
    {
        var payloadObject = RequirePayloadObject(payload, $"RotateJoint step at order index {orderIndex}");

        var jointIndex = RequireInt(payloadObject, "jointIndex", $"RotateJoint step at order index {orderIndex}");
        RequireNumber(payloadObject, "angle", $"RotateJoint step at order index {orderIndex}");

        if (jointIndex is < 0 or > 5)
        {
            throw new InvalidOperationException($"RotateJoint step at order index {orderIndex} jointIndex must be between 0 and 5.");
        }

        ValidatePercentIfPresent(payloadObject, "speed", $"RotateJoint step at order index {orderIndex}");
        ValidatePercentIfPresent(payloadObject, "acc", $"RotateJoint step at order index {orderIndex}");
    }

    private static void ValidateSetDoStepPayload(JsonElement payload, int orderIndex)
    {
        var payloadObject = RequirePayloadObject(payload, $"SetDO step at order index {orderIndex}");

        var doType = RequireString(payloadObject, "doType", $"SetDO step at order index {orderIndex}")
            .Trim()
            .ToLowerInvariant();

        if (doType is not "cabinet" and not "tool")
        {
            throw new InvalidOperationException($"SetDO step at order index {orderIndex} doType must be cabinet or tool.");
        }

        var doIndex = RequireInt(payloadObject, "doIndex", $"SetDO step at order index {orderIndex}");
        var doValue = RequireInt(payloadObject, "doValue", $"SetDO step at order index {orderIndex}");

        if (doType == "cabinet" && doIndex is < 1 or > 8)
        {
            throw new InvalidOperationException($"SetDO cabinet step at order index {orderIndex} doIndex must be between 1 and 8.");
        }

        if (doType == "tool" && doIndex is < 0 or > 1)
        {
            throw new InvalidOperationException($"SetDO tool step at order index {orderIndex} doIndex must be between 0 and 1.");
        }

        if (doValue is not 0 and not 1)
        {
            throw new InvalidOperationException($"SetDO step at order index {orderIndex} doValue must be 0 or 1.");
        }
    }

    private static void ValidateWaitMsStepPayload(JsonElement payload, int orderIndex)
    {
        var payloadObject = RequirePayloadObject(payload, $"WaitMs step at order index {orderIndex}");
        var delayMs = RequireInt(payloadObject, "delayMs", $"WaitMs step at order index {orderIndex}");

        if (delayMs < 0)
        {
            throw new InvalidOperationException($"WaitMs step at order index {orderIndex} delayMs must be greater than or equal to 0.");
        }
    }

    private static JsonElement RequirePayloadObject(JsonElement payload, string scope)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{scope} payload must be an object.");
        }

        return payload;
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

    private static void ApplyStepUpdates(
        RobotProgram program,
        IReadOnlyList<RobotProgramStepRequest> requestedSteps,
        DateTimeOffset now)
    {
        var requestedByOrder = requestedSteps.ToDictionary(step => step.OrderIndex);

        foreach (var existingStep in program.Steps.ToList())
        {
            if (!requestedByOrder.Remove(existingStep.OrderIndex, out var request))
            {
                program.Steps.Remove(existingStep);
                continue;
            }

            if (!Enum.TryParse<RobotProgramStepType>(request.StepType, true, out var stepType))
            {
                throw new InvalidOperationException($"Invalid step type: {request.StepType}.");
            }

            existingStep.StepType = stepType;
            existingStep.Label = request.Label.Trim();
            existingStep.PayloadJson = request.Payload.GetRawText();
        }

        foreach (var request in requestedByOrder.Values.OrderBy(step => step.OrderIndex))
        {
            program.Steps.Add(ToEntity(request, now));
        }
    }

    private static void ValidateProgramName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Program name is required.");
        }
    }

    private static RobotProgramStep ToEntity(
        RobotProgramStepRequest request,
        DateTimeOffset now)
    {
        if (!Enum.TryParse<RobotProgramStepType>(request.StepType, true, out var stepType))
        {
            throw new InvalidOperationException($"Invalid step type: {request.StepType}.");
        }

        return new RobotProgramStep
        {
            Id = Guid.NewGuid(),
            OrderIndex = request.OrderIndex,
            StepType = stepType,
            Label = request.Label.Trim(),
            PayloadJson = request.Payload.GetRawText(),
            CreatedAt = now
        };
    }

    private static ProgramUpdatedEvent ToProgramUpdatedEvent(
    RobotProgram program,
    string changeType)
    {
        return new ProgramUpdatedEvent
        {
            ProgramId = program.Id,
            RobotId = program.RobotId,
            ProgramName = program.Name,
            Status = program.Status.ToString(),
            ChangeType = changeType,
            ChangedAt = DateTimeOffset.UtcNow
        };
    }

    private static RobotProgramResponse ToResponse(RobotProgram program)
    {
        return new RobotProgramResponse
        {
            Id = program.Id,
            RobotId = program.RobotId,
            Name = program.Name,
            Status = program.Status.ToString(),
            Source = program.Source.ToString(),
            CreatedByUserId = program.CreatedByUserId,
            CreatedAt = program.CreatedAt,
            UpdatedAt = program.UpdatedAt,
            Steps = program.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(ToStepResponse)
                .ToList()
        };
    }

    private static RobotProgramStepResponse ToStepResponse(RobotProgramStep step)
    {
        return new RobotProgramStepResponse
        {
            Id = step.Id,
            OrderIndex = step.OrderIndex,
            StepType = step.StepType.ToString(),
            Label = step.Label,
            Payload = string.IsNullOrWhiteSpace(step.PayloadJson)
                ? JsonSerializer.Deserialize<JsonElement>("{}")
                : JsonSerializer.Deserialize<JsonElement>(step.PayloadJson),
            CreatedAt = step.CreatedAt
        };
    }

    private static RobotProgramStatus ParseStatusForCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RobotProgramStatus.Draft;
        }

        if (!Enum.TryParse<RobotProgramStatus>(value, true, out var status))
        {
            throw new InvalidOperationException("Invalid program status.");
        }

        if (status != RobotProgramStatus.Draft)
        {
            throw new InvalidOperationException("New programs must start as Draft. Use publish/archive endpoints to change status.");
        }

        return RobotProgramStatus.Draft;
    }

    private static RobotProgramStatus ParseStatusForUpdate(
        string? value,
        RobotProgramStatus currentStatus)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return currentStatus;
        }

        if (!Enum.TryParse<RobotProgramStatus>(value, true, out var status))
        {
            throw new InvalidOperationException("Invalid program status.");
        }

        if (status != currentStatus)
        {
            throw new InvalidOperationException("Program status cannot be changed through update. Use publish/archive endpoints.");
        }

        return currentStatus;
    }

    private static RobotProgramSource ParseSourceOrDefault(
        string? value,
        RobotProgramSource fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        if (!Enum.TryParse<RobotProgramSource>(value, true, out var source))
        {
            throw new InvalidOperationException("Invalid program source.");
        }

        return source;
    }
}
