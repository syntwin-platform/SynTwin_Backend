namespace Syntwin.Application.Common.Interfaces;

public interface IRobotRuntimeMetrics
{
    void RecordHeartbeat(bool accepted, bool sessionToken);

    void RecordTelemetry(bool accepted, bool sessionToken, bool broadcasted);

    void RecordPendingCommandPoll(bool authenticated, bool delivered, bool waited);

    void RecordCommandResult(bool authenticated, bool accepted, bool duplicate);

    void RecordBackgroundWorkerRun(string workerName, bool lockAcquired, bool succeeded);

    void RecordFactoryRunPreparation(
        double durationMs,
        int targetCount,
        bool succeeded);

    void RecordFactoryRunArmPoll(bool ready);

    void RecordFactoryRunBarrierReady(
        int targetCount,
        double armSpreadMs,
        double leadTimeMs);

    void RecordFactoryRunActualStart(
        int targetCount,
        double startLateByMs);

    void RecordFactoryRunStartSkew(
        int targetCount,
        double skewMs);

    void RecordFactoryRunOutcome(
        string outcomeCode,
        int targetCount);
}
