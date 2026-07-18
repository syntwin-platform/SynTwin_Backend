using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.LuaParsing.Models;
using Syntwin.Application.RobotPrograms.Dtos;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Syntwin.Application.LuaParsing.Services;

public sealed class LuaProgramImportMapper : ILuaProgramImportMapper
{
    private static readonly HashSet<string> ExecutableStepTypes = new(
        ["MoveJ", "MoveL", "SetDO", "WaitMs", "GripperOpen", "GripperClose"],
        StringComparer.OrdinalIgnoreCase);

    public LuaParsePreviewResponse ToPreviewResponse(LuaParseResult parseResult)
    {
        var createProgramRequest = CreateProgramRequest(parseResult);
        var unsupportedSteps = parseResult.Steps
            .Where(step => !ExecutableStepTypes.Contains(step.StepType))
            .OrderBy(step => step.OrderIndex)
            .Select(step => new LuaUnsupportedStep
            {
                OrderIndex = step.OrderIndex,
                StepType = step.StepType,
                Label = step.Label,
                Reason = GetUnsupportedReason(step.StepType)
            })
            .ToList();
        var executionReady = createProgramRequest is not null && unsupportedSteps.Count == 0;

        return new LuaParsePreviewResponse
        {
            Metadata = parseResult.Metadata,
            Variables = parseResult.Variables,
            Points = parseResult.Points,
            ParsedSteps = parseResult.Steps,
            Diagnostics = parseResult.Diagnostics,
            CreateProgramRequest = createProgramRequest,
            ExecutionReady = executionReady,
            CompiledProgramHash = executionReady
                ? ComputeCompiledProgramHash(createProgramRequest!)
                : null,
            UnsupportedSteps = unsupportedSteps
        };
    }

    private static string GetUnsupportedReason(string stepType)
    {
        return stepType switch
        {
            "SetAO" => "SetAO can be preserved in a draft, but runtime execution is not supported yet.",
            "CustomCommand" => "Custom LUA commands can be preserved in a draft, but runtime execution is not supported.",
            _ => $"Step type '{stepType}' is not supported by the runtime executor."
        };
    }

    private static string ComputeCompiledProgramHash(CreateRobotProgramRequest request)
    {
        var compiledJson = JsonSerializer.Serialize(request);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(compiledJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static CreateRobotProgramRequest? CreateProgramRequest(LuaParseResult parseResult)
    {
        if (parseResult.Steps.Count == 0 ||
            parseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == "error"))
        {
            return null;
        }

        return new CreateRobotProgramRequest
        {
            Name = parseResult.Metadata.ProjectName.Trim(),
            Status = "Draft",
            Source = "ImportedLua",
            Steps = parseResult.Steps
                .OrderBy(step => step.OrderIndex)
                .Select(step => new RobotProgramStepRequest
                {
                    OrderIndex = step.OrderIndex,
                    StepType = step.StepType,
                    Label = step.Label,
                    Payload = step.Payload.Clone()
                })
                .ToList()
        };
    }
}
