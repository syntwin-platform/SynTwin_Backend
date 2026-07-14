using System.Text.Json;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Enums;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Policies;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.RobotSafety.Services;

public sealed class RobotSafetyValidationService : IRobotSafetyValidationService
{
    private readonly IRobotSafetyPolicyProvider _policyProvider;

    public RobotSafetyValidationService(IRobotSafetyPolicyProvider policyProvider)
    {
        _policyProvider = policyProvider;
    }

    public async Task<SafetyValidationResult> ValidateProgramAsync(
        RobotSafetyValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new SafetyValidationResult();
        var policy = await _policyProvider.GetPolicyAsync(
            request.RobotId,
            request.CompanyId,
            request.RobotModel,
            cancellationToken);

        if (request.Steps.Count == 0)
        {
            AddBlocker(result, "PROGRAM_EMPTY", null, null, null, "Program must contain at least one step.");
            return result;
        }

        ValidateDuplicateOrderIndexes(request, result);

        var previousJointAngles = NormalizeJointAngles(request.CurrentJointAngles);
        var hasValidatedFirstMoveJ = false;

        foreach (var step in request.Steps.OrderBy(step => step.OrderIndex))
        {
            if (!Enum.TryParse<RobotProgramStepType>(step.StepType, true, out var stepType))
            {
                AddBlocker(
                    result,
                    "STEP_TYPE_INVALID",
                    step,
                    "stepType",
                    $"Unsupported step type '{step.StepType}'.");
                continue;
            }

            switch (stepType)
            {
                case RobotProgramStepType.MoveJ:
                    ValidateMoveJ(step, policy, previousJointAngles, !hasValidatedFirstMoveJ, result, out var moveJoints);
                    if (moveJoints is not null)
                    {
                        previousJointAngles = moveJoints;
                        hasValidatedFirstMoveJ = true;
                    }
                    break;

                case RobotProgramStepType.MoveL:
                case RobotProgramStepType.MoveTCP:
                    ValidateMoveL(step, policy, result);
                    break;

                case RobotProgramStepType.RotateJoint:
                    ValidateRotateJoint(step, policy, result);
                    break;

                case RobotProgramStepType.WaitMs:
                    ValidateWaitMs(step, result);
                    break;

                case RobotProgramStepType.SetDO:
                    ValidateSetDo(step, result);
                    break;

                case RobotProgramStepType.GripperOpen:
                case RobotProgramStepType.GripperClose:
                case RobotProgramStepType.Comment:
                    RequirePayloadObject(step, result);
                    break;

                case RobotProgramStepType.SetAO:
                    AddBlocker(
                        result,
                        "STEP_TYPE_NOT_EXECUTABLE",
                        step,
                        "stepType",
                        "SetAO is preserved for editing but is not supported for execution yet.");
                    break;

                case RobotProgramStepType.CustomCommand:
                    AddBlocker(
                        result,
                        "STEP_TYPE_NOT_EXECUTABLE",
                        step,
                        "stepType",
                        "CustomCommand cannot be executed safely by the backend.");
                    break;

                default:
                    AddBlocker(
                        result,
                        "STEP_TYPE_UNSUPPORTED",
                        step,
                        "stepType",
                        $"Step type '{step.StepType}' is not supported by safety validation.");
                    break;
            }
        }

        return result;
    }

    private static void ValidateDuplicateOrderIndexes(
        RobotSafetyValidationRequest request,
        SafetyValidationResult result)
    {
        var duplicate = request.Steps
            .GroupBy(step => step.OrderIndex)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            AddBlocker(
                result,
                "STEP_ORDER_DUPLICATE",
                duplicate.Key,
                null,
                "orderIndex",
                $"Duplicate step order index: {duplicate.Key}.");
        }
    }

    private static void ValidateMoveJ(
        RobotProgramStepSafetyInput step,
        RobotSafetyPolicyDefinition policy,
        double[]? previousJointAngles,
        bool isFirstMoveJ,
        SafetyValidationResult result,
        out double[]? currentJointAngles)
    {
        currentJointAngles = null;

        if (!TryGetPayloadObject(step, result, out var payload))
        {
            return;
        }

        if (!TryReadNumberArray(payload, "jointAngles", 6, step, result, out var jointAngles))
        {
            return;
        }

        ValidateJointLimits(step, policy, jointAngles, result);
        ValidatePercentIfPresent(payload, "speed", policy.MinSpeedPercent, policy.MaxSpeedPercent, step, result);
        ValidatePercentIfPresent(payload, "acc", policy.MinAccelerationPercent, policy.MaxAccelerationPercent, step, result);

        if (previousJointAngles is not null)
        {
            var maxDelta = isFirstMoveJ
                ? policy.MaxFirstStepJointDeltaDeg
                : policy.MaxJointDeltaDegPerStep;

            ValidateJointDelta(step, previousJointAngles, jointAngles, maxDelta, result);
        }

        currentJointAngles = jointAngles;
    }

    private static void ValidateMoveL(
        RobotProgramStepSafetyInput step,
        RobotSafetyPolicyDefinition policy,
        SafetyValidationResult result)
    {
        if (!TryGetPayloadObject(step, result, out var payload))
        {
            return;
        }

        if (!TryGetObject(payload, "tcpPose", step, result, out var tcpPose))
        {
            return;
        }

        ValidateTcpNumber(tcpPose, "x", policy.TcpWorkspace.MinX, policy.TcpWorkspace.MaxX, step, result);
        ValidateTcpNumber(tcpPose, "y", policy.TcpWorkspace.MinY, policy.TcpWorkspace.MaxY, step, result);
        ValidateTcpNumber(tcpPose, "z", policy.TcpWorkspace.MinZ, policy.TcpWorkspace.MaxZ, step, result);
        ValidateTcpNumber(tcpPose, "rx", policy.TcpWorkspace.MinRotationDeg, policy.TcpWorkspace.MaxRotationDeg, step, result);
        ValidateTcpNumber(tcpPose, "ry", policy.TcpWorkspace.MinRotationDeg, policy.TcpWorkspace.MaxRotationDeg, step, result);
        ValidateTcpNumber(tcpPose, "rz", policy.TcpWorkspace.MinRotationDeg, policy.TcpWorkspace.MaxRotationDeg, step, result);

        ValidatePercentIfPresent(payload, "speed", policy.MinSpeedPercent, policy.MaxSpeedPercent, step, result);
        ValidatePercentIfPresent(payload, "acc", policy.MinAccelerationPercent, policy.MaxAccelerationPercent, step, result);
    }

    private static void ValidateRotateJoint(
        RobotProgramStepSafetyInput step,
        RobotSafetyPolicyDefinition policy,
        SafetyValidationResult result)
    {
        if (!TryGetPayloadObject(step, result, out var payload))
        {
            return;
        }

        if (TryReadInt(payload, "jointIndex", step, result, out var jointIndex) &&
            jointIndex is < 0 or > 5)
        {
            AddBlocker(result, "JOINT_INDEX_OUT_OF_RANGE", step, "jointIndex", "jointIndex must be between 0 and 5.");
        }

        if (TryReadDouble(payload, "angle", step, result, out var angle) &&
            Math.Abs(angle) > policy.MaxJointDeltaDegPerStep)
        {
            AddBlocker(
                result,
                "ROTATE_JOINT_DELTA_TOO_LARGE",
                step,
                "angle",
                $"RotateJoint angle delta must not exceed {policy.MaxJointDeltaDegPerStep} degrees.");
        }

        ValidatePercentIfPresent(payload, "speed", policy.MinSpeedPercent, policy.MaxSpeedPercent, step, result);
        ValidatePercentIfPresent(payload, "acc", policy.MinAccelerationPercent, policy.MaxAccelerationPercent, step, result);
    }

    private static void ValidateWaitMs(
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result)
    {
        if (!TryGetPayloadObject(step, result, out var payload))
        {
            return;
        }

        if (TryReadInt(payload, "delayMs", step, result, out var delayMs) && delayMs < 0)
        {
            AddBlocker(result, "WAIT_DURATION_INVALID", step, "delayMs", "delayMs must be greater than or equal to 0.");
        }
    }

    private static void ValidateSetDo(
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result)
    {
        if (!TryGetPayloadObject(step, result, out var payload))
        {
            return;
        }

        if (!TryReadString(payload, "doType", step, result, out var doType) ||
            !TryReadInt(payload, "doIndex", step, result, out var doIndex) ||
            !TryReadInt(payload, "doValue", step, result, out var doValue))
        {
            return;
        }

        var normalizedDoType = doType.Trim().ToLowerInvariant();

        if (normalizedDoType is not "cabinet" and not "tool")
        {
            AddBlocker(result, "DO_TYPE_INVALID", step, "doType", "doType must be cabinet or tool.");
        }

        if (normalizedDoType == "cabinet" && doIndex is < 1 or > 8)
        {
            AddBlocker(result, "DO_INDEX_INVALID", step, "doIndex", "Cabinet doIndex must be between 1 and 8.");
        }

        if (normalizedDoType == "tool" && doIndex is < 0 or > 1)
        {
            AddBlocker(result, "DO_INDEX_INVALID", step, "doIndex", "Tool doIndex must be between 0 and 1.");
        }

        if (doValue is not 0 and not 1)
        {
            AddBlocker(result, "DO_VALUE_INVALID", step, "doValue", "doValue must be 0 or 1.");
        }
    }

    private static void ValidateJointLimits(
        RobotProgramStepSafetyInput step,
        RobotSafetyPolicyDefinition policy,
        IReadOnlyList<double> jointAngles,
        SafetyValidationResult result)
    {
        for (var index = 0; index < jointAngles.Count; index++)
        {
            var joint = index + 1;
            var limit = policy.JointLimits.FirstOrDefault(limit => limit.Joint == joint);

            if (limit is null)
            {
                AddBlocker(result, "JOINT_LIMIT_MISSING", step, $"jointAngles[{index}]", $"No safety limit is configured for joint {joint}.");
                continue;
            }

            var value = jointAngles[index];

            if (value < limit.MinDeg || value > limit.MaxDeg)
            {
                AddBlocker(
                    result,
                    "JOINT_LIMIT_EXCEEDED",
                    step,
                    $"jointAngles[{index}]",
                    $"Joint {joint} angle {value} is outside allowed range {limit.MinDeg}..{limit.MaxDeg} degrees.");
            }
        }
    }

    private static void ValidateJointDelta(
        RobotProgramStepSafetyInput step,
        IReadOnlyList<double> previous,
        IReadOnlyList<double> current,
        double maxDelta,
        SafetyValidationResult result)
    {
        for (var index = 0; index < current.Count; index++)
        {
            var delta = Math.Abs(current[index] - previous[index]);

            if (delta > maxDelta)
            {
                AddBlocker(
                    result,
                    "JOINT_DELTA_TOO_LARGE",
                    step,
                    $"jointAngles[{index}]",
                    $"Joint {index + 1} changes by {delta:F1} degrees, exceeding the allowed {maxDelta:F1} degrees.");
            }
        }
    }

    private static void ValidateTcpNumber(
        JsonElement tcpPose,
        string field,
        double min,
        double max,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result)
    {
        if (!TryReadDouble(tcpPose, field, step, result, out var value, $"tcpPose.{field}"))
        {
            return;
        }

        if (value < min || value > max)
        {
            AddBlocker(
                result,
                "TCP_WORKSPACE_EXCEEDED",
                step,
                $"tcpPose.{field}",
                $"tcpPose.{field} value {value} is outside allowed range {min}..{max}.");
        }
    }

    private static void ValidatePercentIfPresent(
        JsonElement payload,
        string propertyName,
        int min,
        int max,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result)
    {
        if (!payload.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number)
        {
            AddBlocker(result, "PERCENT_INVALID", step, propertyName, $"{propertyName} must be a number.");
            return;
        }

        var number = value.GetDouble();

        if (number < min || number > max)
        {
            AddBlocker(result, "PERCENT_OUT_OF_RANGE", step, propertyName, $"{propertyName} must be between {min} and {max}.");
        }
    }

    private static bool RequirePayloadObject(
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result)
    {
        return TryGetPayloadObject(step, result, out _);
    }

    private static bool TryGetPayloadObject(
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out JsonElement payload)
    {
        payload = step.Payload;

        if (payload.ValueKind != JsonValueKind.Object)
        {
            AddBlocker(result, "PAYLOAD_INVALID", step, "payload", "Step payload must be a JSON object.");
            return false;
        }

        return true;
    }

    private static bool TryGetObject(
        JsonElement payload,
        string propertyName,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out JsonElement value)
    {
        if (!payload.TryGetProperty(propertyName, out value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            AddBlocker(result, "PAYLOAD_FIELD_MISSING", step, propertyName, $"{propertyName} must be a JSON object.");
            return false;
        }

        return true;
    }

    private static bool TryReadNumberArray(
        JsonElement payload,
        string propertyName,
        int expectedLength,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out double[] values)
    {
        values = Array.Empty<double>();

        if (!payload.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            AddBlocker(result, "PAYLOAD_FIELD_MISSING", step, propertyName, $"{propertyName} must be an array.");
            return false;
        }

        if (array.GetArrayLength() != expectedLength)
        {
            AddBlocker(result, "ARRAY_LENGTH_INVALID", step, propertyName, $"{propertyName} must contain exactly {expectedLength} values.");
            return false;
        }

        var numbers = new List<double>();

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number)
            {
                AddBlocker(result, "ARRAY_VALUE_INVALID", step, propertyName, $"{propertyName} must contain only numbers.");
                return false;
            }

            numbers.Add(item.GetDouble());
        }

        values = numbers.ToArray();
        return true;
    }

    private static bool TryReadDouble(
        JsonElement payload,
        string propertyName,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out double value,
        string? fieldName = null)
    {
        value = 0;
        fieldName ??= propertyName;

        if (!payload.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Number)
        {
            AddBlocker(result, "PAYLOAD_FIELD_MISSING", step, fieldName, $"{fieldName} must be a number.");
            return false;
        }

        value = element.GetDouble();
        return true;
    }

    private static bool TryReadInt(
        JsonElement payload,
        string propertyName,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out int value)
    {
        value = 0;

        if (!payload.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Number ||
            !element.TryGetInt32(out value))
        {
            AddBlocker(result, "PAYLOAD_FIELD_MISSING", step, propertyName, $"{propertyName} must be an integer.");
            return false;
        }

        return true;
    }

    private static bool TryReadString(
        JsonElement payload,
        string propertyName,
        RobotProgramStepSafetyInput step,
        SafetyValidationResult result,
        out string value)
    {
        value = string.Empty;

        if (!payload.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(element.GetString()))
        {
            AddBlocker(result, "PAYLOAD_FIELD_MISSING", step, propertyName, $"{propertyName} must be a non-empty string.");
            return false;
        }

        value = element.GetString()!;
        return true;
    }

    private static double[]? NormalizeJointAngles(IReadOnlyList<double>? jointAngles)
    {
        return jointAngles is { Count: 6 }
            ? jointAngles.ToArray()
            : null;
    }

    private static void AddBlocker(
        SafetyValidationResult result,
        string code,
        RobotProgramStepSafetyInput? step,
        string? field,
        string message)
    {
        AddBlocker(
            result,
            code,
            step?.OrderIndex,
            step?.Label,
            field,
            message);
    }

    private static void AddBlocker(
        SafetyValidationResult result,
        string code,
        int? stepOrderIndex,
        string? stepLabel,
        string? field,
        string message)
    {
        result.Diagnostics.Add(new SafetyDiagnostic
        {
            Severity = SafetySeverity.Blocker,
            Code = code,
            StepOrderIndex = stepOrderIndex,
            StepLabel = stepLabel,
            Field = field,
            Message = message
        });
    }
}