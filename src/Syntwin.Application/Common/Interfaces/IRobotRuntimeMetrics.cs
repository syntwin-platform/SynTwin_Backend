namespace Syntwin.Application.Common.Interfaces;

public interface IRobotRuntimeMetrics
{
    void RecordHeartbeat(bool accepted, bool sessionToken);

    void RecordTelemetry(bool accepted, bool sessionToken, bool broadcasted);

    void RecordPendingCommandPoll(bool authenticated, bool delivered, bool waited);

    void RecordCommandResult(bool authenticated, bool accepted, bool duplicate);

    void RecordBackgroundWorkerRun(string workerName, bool lockAcquired, bool succeeded);
}