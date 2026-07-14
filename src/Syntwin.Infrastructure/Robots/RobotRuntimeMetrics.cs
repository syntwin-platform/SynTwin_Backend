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

    private static readonly Histogram<double> FactoryRunPreparationDuration =
        Meter.CreateHistogram<double>(
            "syntwin_factory_run_preparation_duration_ms",
            unit: "ms");

    private static readonly Counter<long> FactoryRunArmPolls = Meter.CreateCounter<long>(
        "syntwin_factory_run_arm_polls_total");

    private static readonly Histogram<double> FactoryRunArmSpread =
        Meter.CreateHistogram<double>(
            "syntwin_factory_run_arm_spread_ms",
            unit: "ms");

    private static readonly Histogram<double> FactoryRunStartLeadTime =
        Meter.CreateHistogram<double>(
            "syntwin_factory_run_start_lead_time_ms",
            unit: "ms");

    private static readonly Histogram<double> FactoryRunStartLateness =
        Meter.CreateHistogram<double>(
            "syntwin_factory_run_start_lateness_ms",
            unit: "ms");

    private static readonly Histogram<double> FactoryRunStartSkew =
        Meter.CreateHistogram<double>(
            "syntwin_factory_run_start_skew_ms",
            unit: "ms");

    private static readonly Counter<long> FactoryRunOutcomes = Meter.CreateCounter<long>(
        "syntwin_factory_run_outcomes_total");

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

    public void RecordFactoryRunPreparation(
        double durationMs,
        int targetCount,
        bool succeeded)
    {
        FactoryRunPreparationDuration.Record(
            Math.Max(0, durationMs),
            new KeyValuePair<string, object?>("target_count", targetCount),
            new KeyValuePair<string, object?>("succeeded", succeeded));
    }

    public void RecordFactoryRunArmPoll(bool ready)
    {
        FactoryRunArmPolls.Add(
            1,
            new KeyValuePair<string, object?>("ready", ready));
    }

    public void RecordFactoryRunBarrierReady(
        int targetCount,
        double armSpreadMs,
        double leadTimeMs)
    {
        var targetCountTag = new KeyValuePair<string, object?>(
            "target_count",
            targetCount);

        FactoryRunArmSpread.Record(Math.Max(0, armSpreadMs), targetCountTag);
        FactoryRunStartLeadTime.Record(Math.Max(0, leadTimeMs), targetCountTag);
    }

    public void RecordFactoryRunActualStart(
        int targetCount,
        double startLateByMs)
    {
        FactoryRunStartLateness.Record(
            Math.Max(0, startLateByMs),
            new KeyValuePair<string, object?>("target_count", targetCount));
    }

    public void RecordFactoryRunStartSkew(
        int targetCount,
        double skewMs)
    {
        FactoryRunStartSkew.Record(
            Math.Max(0, skewMs),
            new KeyValuePair<string, object?>("target_count", targetCount));
    }

    public void RecordFactoryRunOutcome(
        string outcomeCode,
        int targetCount)
    {
        FactoryRunOutcomes.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcomeCode),
            new KeyValuePair<string, object?>("target_count", targetCount));
    }
}
