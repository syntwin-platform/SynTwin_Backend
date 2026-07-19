using Syntwin.Application.LuaParsing.Services;

namespace Syntwin.Application.Tests.LuaParsing;

public sealed class LuaExecutionContractTests
{
    private readonly LuaProgramParser _parser = new();
    private readonly LuaProgramImportMapper _mapper = new();

    [Fact]
    public void SupportedProgram_IsExecutionReadyAndHasStableCompiledHash()
    {
        const string lua =
            "MoveJ({0, 0, 0, 0, 0, 0}, 0, 0, 30, 30, -1, -1)";

        var first = _mapper.ToPreviewResponse(_parser.Parse(lua, "supported.lua"));
        var second = _mapper.ToPreviewResponse(_parser.Parse(lua, "supported.lua"));

        Assert.True(first.ExecutionReady);
        Assert.Empty(first.UnsupportedSteps);
        Assert.NotNull(first.CreateProgramRequest);
        Assert.NotNull(first.CompiledProgramHash);
        Assert.Equal(64, first.CompiledProgramHash.Length);
        Assert.Equal(first.CompiledProgramHash, second.CompiledProgramHash);
    }

    [Theory]
    [InlineData("SetAO(0, 1)", "SetAO")]
    [InlineData("UnknownRobotCommand(1, 2, 3)", "CustomCommand")]
    public void DraftOnlyStep_IsRejectedByExecutionContract(
        string lua,
        string expectedStepType)
    {
        var preview = _mapper.ToPreviewResponse(_parser.Parse(lua, "draft-only.lua"));

        Assert.False(preview.ExecutionReady);
        Assert.Null(preview.CompiledProgramHash);
        Assert.NotNull(preview.CreateProgramRequest);
        var unsupported = Assert.Single(preview.UnsupportedSteps);
        Assert.Equal(expectedStepType, unsupported.StepType);
        Assert.False(string.IsNullOrWhiteSpace(unsupported.Reason));
    }

    [Fact]
    public void ParseError_IsNotExecutionReady()
    {
        const string lua = "MoveJ({0, 1, 2}, 0, 0, 30, 30, -1, -1)";

        var preview = _mapper.ToPreviewResponse(_parser.Parse(lua, "invalid.lua"));

        Assert.False(preview.ExecutionReady);
        Assert.Null(preview.CompiledProgramHash);
        Assert.Null(preview.CreateProgramRequest);
        Assert.Contains(preview.Diagnostics, diagnostic => diagnostic.Severity == "error");
    }

    [Fact]
    public void RecordedTraceMetadata_IsPreservedForMoveLExecution()
    {
        const string lua = """
            -- @FAIROBOT_TRACE {"version":1,"groupId":"trace-a","sampleIndex":0,"sampleCount":2,"segmentDurationMs":0,"jointAngles":[0,-20,30,-40,-90,0]}
            MoveJ({0, -20, 30, -40, -90, 0}, 0, 0, 30, 30, -1, -1)
            -- @FAIROBOT_TRACE {"version":1,"groupId":"trace-a","sampleIndex":1,"sampleCount":2,"segmentDurationMs":42,"jointAngles":[1,-19,31,-39,-89,1]}
            MoveL({100, 200, 300, 0, 90, 0}, 0, 0, 30, 30, -1, -1)
            """;

        var parsed = _parser.Parse(lua, "trace.lua");

        Assert.Empty(parsed.Diagnostics);
        Assert.Equal(2, parsed.Steps.Count);

        var moveJTrace = parsed.Steps[0].Payload.GetProperty("trace");
        Assert.Equal("trace-a", moveJTrace.GetProperty("groupId").GetString());
        Assert.Equal(0, moveJTrace.GetProperty("sampleIndex").GetInt32());

        var moveLPayload = parsed.Steps[1].Payload;
        Assert.Equal(42, moveLPayload.GetProperty("trace").GetProperty("segmentDurationMs").GetDouble());
        Assert.Equal(
            new[] { 1d, -19d, 31d, -39d, -89d, 1d },
            moveLPayload.GetProperty("recordedJointAngles")
                .EnumerateArray()
                .Select(value => value.GetDouble())
                .ToArray());
    }

    [Fact]
    public void MalformedRecordedTraceMetadata_IsRejected()
    {
        const string lua = """
            -- @FAIROBOT_TRACE {"version":1,"groupId":"trace-a","sampleIndex":1,"sampleCount":2,"segmentDurationMs":42,"jointAngles":[1,2]}
            MoveL({100, 200, 300, 0, 90, 0}, 0, 0, 30, 30, -1, -1)
            """;

        var preview = _mapper.ToPreviewResponse(_parser.Parse(lua, "invalid-trace.lua"));

        Assert.False(preview.ExecutionReady);
        Assert.Contains(preview.Diagnostics, diagnostic =>
            diagnostic.Severity == "error" &&
            diagnostic.Message.Contains("@FAIROBOT_TRACE", StringComparison.Ordinal));
    }

    [Fact]
    public void SingleSegmentRecordedTraceMetadata_IsAccepted()
    {
        const string lua = """
            -- @FAIROBOT_TRACE {"version":1,"groupId":"trace-single","sampleIndex":0,"sampleCount":1,"segmentDurationMs":42,"jointAngles":[1,-19,31,-39,-89,1]}
            MoveL({100, 200, 300, 0, 90, 0}, 0, 0, 30, 30, -1, -1)
            """;

        var parsed = _parser.Parse(lua, "single-trace.lua");
        var preview = _mapper.ToPreviewResponse(parsed);

        Assert.Empty(parsed.Diagnostics);
        Assert.True(preview.ExecutionReady);
        Assert.Single(parsed.Steps);
    }
}
