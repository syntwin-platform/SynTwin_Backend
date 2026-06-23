using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.LuaParsing.Models;
using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.LuaParsing.Services;

public sealed class LuaProgramImportMapper : ILuaProgramImportMapper
{
    public LuaParsePreviewResponse ToPreviewResponse(LuaParseResult parseResult)
    {
        return new LuaParsePreviewResponse
        {
            Metadata = parseResult.Metadata,
            Variables = parseResult.Variables,
            Points = parseResult.Points,
            ParsedSteps = parseResult.Steps,
            Diagnostics = parseResult.Diagnostics,
            CreateProgramRequest = CreateProgramRequest(parseResult)
        };
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