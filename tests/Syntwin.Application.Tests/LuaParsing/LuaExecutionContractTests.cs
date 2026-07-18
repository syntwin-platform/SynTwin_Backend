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
}
