using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Domain.Enums;
using Syntwin.Integration.Tests.Infrastructure;
using Xunit;

namespace Syntwin.Integration.Tests.FactoryRuns;

[Collection(IntegrationTestCollection.Name)]
public sealed class FactoryRunApiDatabaseIntegrationTests(FactoryRunApiFixture fixture)
{
    private const string LuaA = "MoveJ({0, 0, 0, 0, 0, 0}, 0, 0, 30, 30, -1, -1)";
    private const string LuaB = "WaitMs(250)";

    [Fact]
    public async Task LegacyContract_ReachesDatabaseWithBackwardCompatiblePolicies()
    {
        await fixture.ResetAsync();
        var request = new CreateFactoryRunRequest
        {
            CompanyId = FactoryRunApiFixture.CompanyId,
            ProgramName = "legacy",
            LuaFileName = "legacy.lua",
            LuaContent = LuaA,
            RobotIds = [FactoryRunApiFixture.RobotOneId]
        };

        var response = await fixture.Client.PostAsJsonAsync("/api/factory-runs", request);
        var run = await ReadRunAsync(response, HttpStatusCode.Created);

        Assert.Equal("Synchronized", run.CoordinationMode);
        Assert.Equal("IsolateTarget", run.FailurePolicy);
        Assert.Single(run.Programs);
        Assert.Single(run.Targets);
        Assert.True(run.Targets[0].FactoryRunProgramId.HasValue);

        var persisted = await fixture.WithDbContextAsync(async db => new
        {
            Runs = await db.FactoryRuns.CountAsync(),
            Programs = await db.FactoryRunPrograms.CountAsync(),
            Targets = await db.FactoryRunTargets.CountAsync()
        });
        Assert.Equal(1, persisted.Runs);
        Assert.Equal(1, persisted.Programs);
        Assert.Equal(1, persisted.Targets);
    }

    [Fact]
    public async Task SameLuaForTwoTargets_CreatesOneImmutableCompiledSnapshot()
    {
        await fixture.ResetAsync();
        var request = CreatePerTargetRequest(
            ("pick-a", LuaA, FactoryRunApiFixture.RobotOneId),
            ("pick-b", LuaA, FactoryRunApiFixture.RobotTwoId));

        var response = await fixture.Client.PostAsJsonAsync("/api/factory-runs", request);
        var run = await ReadRunAsync(response, HttpStatusCode.Created);

        var program = Assert.Single(run.Programs);
        Assert.Equal(2, run.Targets.Count);
        Assert.All(run.Targets, target => Assert.Equal(program.Id, target.FactoryRunProgramId));

        var snapshot = await fixture.WithDbContextAsync(async db =>
            await db.FactoryRunPrograms
                .Where(item => item.FactoryRunId == run.Id)
                .Select(item => new { item.CompiledProgramJson, item.CompiledProgramHash })
                .SingleAsync());
        Assert.False(string.IsNullOrWhiteSpace(snapshot.CompiledProgramJson));
        Assert.Equal(64, snapshot.CompiledProgramHash?.Length);
    }

    [Fact]
    public async Task DifferentLuaPerTarget_CreatesTwoIndependentCompiledSnapshots()
    {
        await fixture.ResetAsync();
        var request = CreatePerTargetRequest(
            ("pick", LuaA, FactoryRunApiFixture.RobotOneId),
            ("wait", LuaB, FactoryRunApiFixture.RobotTwoId));

        var response = await fixture.Client.PostAsJsonAsync("/api/factory-runs", request);
        var run = await ReadRunAsync(response, HttpStatusCode.Created);

        Assert.Equal(2, run.Programs.Count);
        Assert.Equal(2, run.Targets.Select(target => target.FactoryRunProgramId).Distinct().Count());

        var hashes = await fixture.WithDbContextAsync(async db =>
            await db.FactoryRunPrograms
                .Where(item => item.FactoryRunId == run.Id)
                .Select(item => item.CompiledProgramHash)
                .ToListAsync());
        Assert.Equal(2, hashes.Distinct().Count());
        Assert.All(hashes, hash => Assert.Equal(64, hash?.Length));
    }

    [Fact]
    public async Task RetriedCreateWithSameClientRequestId_IsIdempotent()
    {
        await fixture.ResetAsync();
        var request = CreatePerTargetRequest(
            ("pick", LuaA, FactoryRunApiFixture.RobotOneId),
            ("wait", LuaB, FactoryRunApiFixture.RobotTwoId));
        request.ClientRequestId = Guid.NewGuid();

        var first = await ReadRunAsync(
            await fixture.Client.PostAsJsonAsync("/api/factory-runs", request),
            HttpStatusCode.Created);
        var second = await ReadRunAsync(
            await fixture.Client.PostAsJsonAsync("/api/factory-runs", request),
            HttpStatusCode.Created);

        Assert.Equal(first.Id, second.Id);
        var counts = await fixture.WithDbContextAsync(async db => new
        {
            Runs = await db.FactoryRuns.CountAsync(),
            Programs = await db.FactoryRunPrograms.CountAsync(),
            Targets = await db.FactoryRunTargets.CountAsync()
        });
        Assert.Equal(1, counts.Runs);
        Assert.Equal(2, counts.Programs);
        Assert.Equal(2, counts.Targets);
    }

    [Fact]
    public async Task IsolatedPreparationFailure_DoesNotRollbackPreparedTargetOrDuplicateRetry()
    {
        await fixture.ResetAsync();
        fixture.FailurePlan.ProgramPreparationFailures.Add(FactoryRunApiFixture.RobotTwoId);
        var create = CreatePerTargetRequest(
            ("pick", LuaA, FactoryRunApiFixture.RobotOneId),
            ("wait", LuaB, FactoryRunApiFixture.RobotTwoId));
        var run = await ReadRunAsync(
            await fixture.Client.PostAsJsonAsync("/api/factory-runs", create),
            HttpStatusCode.Created);

        var firstPrepare = await ReadRunAsync(
            await fixture.Client.PostAsync($"/api/factory-runs/{run.Id}/prepare", null),
            HttpStatusCode.OK);
        var successful = Assert.Single(
            firstPrepare.Targets,
            target => target.RobotId == FactoryRunApiFixture.RobotOneId);
        var failed = Assert.Single(
            firstPrepare.Targets,
            target => target.RobotId == FactoryRunApiFixture.RobotTwoId);
        Assert.NotEqual("Failed", successful.Status);
        Assert.True(successful.ProgramId.HasValue);
        Assert.True(successful.PrepareCommandId.HasValue);
        Assert.Equal("Failed", failed.Status);
        Assert.Contains("Injected preparation failure", failed.FailureReason);

        await ReadRunAsync(
            await fixture.Client.PostAsync($"/api/factory-runs/{run.Id}/prepare", null),
            HttpStatusCode.OK);

        var persisted = await fixture.WithDbContextAsync(async db => new
        {
            Programs = await db.RobotPrograms.CountAsync(),
            PrepareCommands = await db.RobotCommands
                .CountAsync(command => command.CommandType == RobotCommandType.PrepareProgram)
        });
        Assert.Equal(1, persisted.Programs);
        Assert.Equal(1, persisted.PrepareCommands);
    }

    [Fact]
    public async Task IndependentStartDispatchFailure_PreservesSuccessfullyStartedTarget()
    {
        await fixture.ResetAsync();
        var create = CreatePerTargetRequest(
            ("pick", LuaA, FactoryRunApiFixture.RobotOneId),
            ("wait", LuaB, FactoryRunApiFixture.RobotTwoId));
        var run = await ReadRunAsync(
            await fixture.Client.PostAsJsonAsync("/api/factory-runs", create),
            HttpStatusCode.Created);
        var prepared = await ReadRunAsync(
            await fixture.Client.PostAsync($"/api/factory-runs/{run.Id}/prepare", null),
            HttpStatusCode.OK);

        await fixture.WithDbContextAsync(async db =>
        {
            var targets = await db.FactoryRunTargets
                .Where(target => target.FactoryRunId == run.Id)
                .ToListAsync();
            foreach (var target in targets)
            {
                target.Status = FactoryRunTargetStatus.Ready;
                target.ReadyAtUtc = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync();
            return true;
        });

        fixture.CommandQueue.Reset();
        fixture.FailurePlan.QueueDispatchFailures.Add(FactoryRunApiFixture.RobotTwoId);
        var started = await ReadRunAsync(
            await fixture.Client.PostAsync($"/api/factory-runs/{run.Id}/start", null),
            HttpStatusCode.OK);

        var dispatched = Assert.Single(
            started.Targets,
            target => target.RobotId == FactoryRunApiFixture.RobotOneId);
        var failed = Assert.Single(
            started.Targets,
            target => target.RobotId == FactoryRunApiFixture.RobotTwoId);
        Assert.NotEqual("Failed", dispatched.Status);
        Assert.True(dispatched.CommandId.HasValue);
        Assert.Equal("Failed", failed.Status);
        Assert.Contains("Queue unavailable", failed.FailureReason);
        Assert.Single(fixture.CommandQueue.CommandIds);

        var commandCountBeforeRetry = await fixture.WithDbContextAsync(
            db => db.RobotCommands.CountAsync());
        await ReadRunAsync(
            await fixture.Client.PostAsync($"/api/factory-runs/{run.Id}/start", null),
            HttpStatusCode.OK);
        var commandCountAfterRetry = await fixture.WithDbContextAsync(
            db => db.RobotCommands.CountAsync());
        Assert.Equal(commandCountBeforeRetry, commandCountAfterRetry);
    }

    private static CreateFactoryRunRequest CreatePerTargetRequest(
        params (string Key, string Lua, Guid RobotId)[] assignments)
    {
        return new CreateFactoryRunRequest
        {
            CompanyId = FactoryRunApiFixture.CompanyId,
            CoordinationMode = FactoryCoordinationMode.ParallelIndependent,
            FailurePolicy = FactoryFailurePolicy.IsolateTarget,
            Programs = assignments.Select(item => new CreateFactoryRunProgramRequest
            {
                Key = item.Key,
                ProgramName = item.Key,
                LuaFileName = $"{item.Key}.lua",
                LuaContent = item.Lua
            }).ToList(),
            Targets = assignments.Select(item => new CreateFactoryRunTargetRequest
            {
                RobotId = item.RobotId,
                ProgramKey = item.Key
            }).ToList()
        };
    }

    private static async Task<FactoryRunResponse> ReadRunAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == expectedStatus,
            $"Expected {(int)expectedStatus}, received {(int)response.StatusCode}: {body}");
        var run = await response.Content.ReadFromJsonAsync<FactoryRunResponse>();
        return Assert.IsType<FactoryRunResponse>(run);
    }
}
