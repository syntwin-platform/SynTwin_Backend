using System.Diagnostics.Metrics;
using Syntwin.Application.Common.Interfaces;

namespace Syntwin.Infrastructure.Robots;

public sealed class RobotRuntimeMetrics : IRobotRuntimeMetrics
{
    private static readonly Meter Meter = new("Syntwin.RobotRuntime", "1.0.0");

    private static readonly Counter<long> Heartbeats = Meter.CreateCounter<long>(
        "syntwin_robot_heartbeats_total");

    private static readonly Counter<long> TelemetrySubmissions = Meter.CreateCounter<long>(
        "syntwin_robot_telemetry_submissions_total");

    private static readonly Counter<long> PendingCommandPolls = Meter.CreateCounter<long>(
        "syntwin_robot_pending_command_polls_total");

    private static readonly Counter<long> CommandResults = Meter.CreateCounter<long>(
        "syntwin_robot_command_results_total");

    private static readonly Counter<long> BackgroundWorkerRuns = Meter.CreateCounter<long>(
        "syntwin_robot_background_worker_runs_total");

    public void RecordHeartbeat(bool accepted, bool sessionToken)
    {
        Heartbeats.Add(
            1,
            new KeyValuePair<string, object?>("accepted", accepted),
            new KeyValuePair<string, object?>("auth_type", sessionToken ? "session" : "legacy"));
    }

    public void RecordTelemetry(bool accepted, bool sessionToken, bool broadcasted)
    {
        TelemetrySubmissions.Add(
            1,
            new KeyValuePair<string, object?>("accepted", accepted),
            new KeyValuePair<string, object?>("auth_type", sessionToken ? "session" : "legacy"),
            new KeyValuePair<string, object?>("broadcasted", broadcasted));
    }

    public void RecordPendingCommandPoll(bool authenticated, bool delivered, bool waited)
    {
        PendingCommandPolls.Add(
            1,
            new KeyValuePair<string, object?>("authenticated", authenticated),
            new KeyValuePair<string, object?>("delivered", delivered),
            new KeyValuePair<string, object?>("waited", waited));
    }

    public void RecordCommandResult(bool authenticated, bool accepted, bool duplicate)
    {
        CommandResults.Add(
            1,
            new KeyValuePair<string, object?>("authenticated", authenticated),
            new KeyValuePair<string, object?>("accepted", accepted),
            new KeyValuePair<string, object?>("duplicate", duplicate));
    }

    public void RecordBackgroundWorkerRun(string workerName, bool lockAcquired, bool succeeded)
    {
        BackgroundWorkerRuns.Add(
            1,
            new KeyValuePair<string, object?>("worker", workerName),
            new KeyValuePair<string, object?>("lock_acquired", lockAcquired),
            new KeyValuePair<string, object?>("succeeded", succeeded));
    }
}