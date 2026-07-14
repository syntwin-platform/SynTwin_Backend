namespace Syntwin.Domain.Enums;

public enum FactoryRunTargetStatus
{
    Pending = 1,
    Preparing = 2,
    Prepared = 3,
    WaitingForDeviceReady = 4,
    Ready = 5,
    Starting = 6,
    Armed = 7,
    Running = 8,
    Completed = 9,
    Failed = 10,
    Cancelled = 11
}