namespace Syntwin.Application.Robots.Options;

public sealed class RobotRuntimeOptions
{
    public int OnlineTtlSeconds { get; set; } = 30;

    public int LastSeenTtlSeconds { get; set; } = 300;

    public int RuntimeSessionTtlSeconds { get; set; } = 3600;

    public int DeviceSessionTtlSeconds { get; set; } = 3600;

    public int LatestStateTtlSeconds { get; set; } = 120;

    public int TelemetryBroadcastMinIntervalMilliseconds { get; set; } = 200;

    public int OfflineMonitorIntervalSeconds { get; set; } = 5;

    public int CommandTimeoutMonitorIntervalSeconds { get; set; } = 10;

    public int LastSeenFlushIntervalSeconds { get; set; } = 60;

    public int LastSeenFlushBatchSize { get; set; } = 100;

    public int RobotBusyLockTtlSeconds { get; set; } = 300;

    public int FactoryRunBusyLockTtlSeconds { get; set; } = 900;

    public int FactoryRunLockMaintenanceIntervalSeconds { get; set; } = 30;

    public bool FactoryRunBarrierV2Enabled { get; set; } = true;

    public int FactoryRunBarrierTtlSeconds { get; set; } = 900;

    public int FactoryRunBarrierSealLeaseSeconds { get; set; } = 30;

    public int FactoryRunProgramPreparationMaxConcurrency { get; set; } = 6;

    public int FactoryRunStartPropagationBaseMilliseconds { get; set; } = 750;

    public int FactoryRunStartPropagationPerTargetMilliseconds { get; set; } = 50;

    public int FactoryRunStartArmSpreadContributionMaxMilliseconds { get; set; } = 1000;

    public int FactoryRunStartLeadTimeMaxMilliseconds { get; set; } = 5000;

    public int PendingCommandPollIntervalMilliseconds { get; set; } = 250;

    public int PendingCommandMaxWaitSeconds { get; set; } = 25;

    public int PendingCommandMaxSkippedQueueItems { get; set; } = 20;

    public bool AllowLegacyDeviceSecretAuth { get; set; } = false;

    public int OfflineMonitorLockTtlSeconds { get; set; } = 30;

    public int CommandTimeoutMonitorLockTtlSeconds { get; set; } = 30;

    public int LastSeenFlushLockTtlSeconds { get; set; } = 120;
}
