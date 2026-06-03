using System.Text.Json;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.RobotPrograms.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;


namespace Syntwin.Application.RobotPrograms.Services;

public sealed class RobotProgramService : IRobotProgramService
{
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotProgramRepository _programRepository;
    

    public RobotProgramService(
        IRobotRepository robotRepository,
        IRobotProgramRepository programRepository)
    {
        _robotRepository = robotRepository;
        _programRepository = programRepository;
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
            if (!Enum.TryParse<RobotProgramStepType>(step.StepType, true, out _))
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
