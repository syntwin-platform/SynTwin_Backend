namespace Syntwin.Domain.Enums;

public enum FactoryRunStatus
{
    Created = 1,
    Preparing = 2,
    Prepared = 3,
    WaitingForReady = 4,
    Ready = 5,
    Starting = 6,
    Running = 7,
    Completed = 8,
    Failed = 9,
    Cancelling = 10,
    Cancelled = 11,
    RunningDegraded = 12,
    PartiallyCompleted = 13
}
