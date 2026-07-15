using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.Robots.Options;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Syntwin.Application.Tests.FactoryRuns;

public sealed class FactoryRunContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void OldCreateRequest_UsesBackwardCompatibleDefaults()
    {
        const string json =
            """
            {
              "companyId": "11111111-1111-1111-1111-111111111111",
              "programName": "legacy_factory_program",
              "luaFileName": "legacy_factory_program.lua",
              "luaContent": "print('legacy')",
              "robotIds": [
                "22222222-2222-2222-2222-222222222222"
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<CreateFactoryRunRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(
            FactoryCoordinationMode.Synchronized,
            request.CoordinationMode);
        Assert.Equal(
            FactoryFailurePolicy.IsolateTarget,
            request.FailurePolicy);
    }

    [Fact]
    public void NewCreateRequest_DeserializesExplicitPolicies()
    {
        const string json =
            """
            {
              "companyId": "11111111-1111-1111-1111-111111111111",
              "coordinationMode": "ParallelIndependent",
              "failurePolicy": "AbortExecutionGroup",
              "programName": "independent_factory_program",
              "luaFileName": "independent_factory_program.lua",
              "luaContent": "print('independent')",
              "robotIds": [
                "22222222-2222-2222-2222-222222222222",
                "33333333-3333-3333-3333-333333333333"
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<CreateFactoryRunRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(
            FactoryCoordinationMode.ParallelIndependent,
            request.CoordinationMode);
        Assert.Equal(
            FactoryFailurePolicy.AbortExecutionGroup,
            request.FailurePolicy);
    }

    [Fact]
    public void PerTargetCreateRequest_DeserializesProgramsAndAssignments()
    {
        const string json =
            """
            {
              "companyId": "11111111-1111-1111-1111-111111111111",
              "coordinationMode": "ParallelIndependent",
              "failurePolicy": "IsolateTarget",
              "programs": [
                {
                  "key": "pick-source",
                  "programName": "Pick",
                  "luaFileName": "pick.lua",
                  "luaContent": "print('pick')"
                },
                {
                  "key": "place-source",
                  "programName": "Place",
                  "luaFileName": "place.lua",
                  "luaContent": "print('place')"
                }
              ],
              "targets": [
                {
                  "robotId": "22222222-2222-2222-2222-222222222222",
                  "programKey": "pick-source"
                },
                {
                  "robotId": "33333333-3333-3333-3333-333333333333",
                  "programKey": "place-source"
                }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<CreateFactoryRunRequest>(json, JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(2, request.Programs.Count);
        Assert.Equal(2, request.Targets.Count);
        Assert.Equal("pick-source", request.Targets[0].ProgramKey);
        Assert.Equal("place-source", request.Targets[1].ProgramKey);
        Assert.Empty(request.RobotIds);
        Assert.Null(request.LuaContent);
    }

    [Fact]
    public void NewFactoryRunEntity_UsesSafeDefaults()
    {
        var factoryRun = new FactoryRun();

        Assert.Equal(
            FactoryCoordinationMode.Synchronized,
            factoryRun.CoordinationMode);
        Assert.Equal(
            FactoryFailurePolicy.IsolateTarget,
            factoryRun.FailurePolicy);
    }

    [Fact]
    public void DegradedStatuses_AreAppendedWithoutChangingExistingValues()
    {
        Assert.Equal(11, (int)FactoryRunStatus.Cancelled);
        Assert.Equal(12, (int)FactoryRunStatus.RunningDegraded);
        Assert.Equal(13, (int)FactoryRunStatus.PartiallyCompleted);
    }

    [Fact]
    public void PreparedProgramResult_DistinguishesSuccessFromIsolatedFailure()
    {
        var success = new FactoryRunPreparedProgram
        {
            RobotId = Guid.NewGuid(),
            Program = new RobotProgramResponse()
        };
        var failure = new FactoryRunPreparedProgram
        {
            RobotId = Guid.NewGuid(),
            Error = "Create/Publish failed."
        };

        Assert.True(success.IsSuccess);
        Assert.False(failure.IsSuccess);
    }

    [Fact]
    public void RuntimeOptions_KeepFactoryLocksAliveLongerThanMaintenanceInterval()
    {
        var options = new RobotRuntimeOptions();

        Assert.True(options.FactoryRunBusyLockTtlSeconds > 0);
        Assert.True(options.FactoryRunLockMaintenanceIntervalSeconds > 0);
        Assert.True(
            options.FactoryRunBusyLockTtlSeconds >=
            options.FactoryRunLockMaintenanceIntervalSeconds * 3);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}
