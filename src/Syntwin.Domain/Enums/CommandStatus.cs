namespace Syntwin.Domain.Enums;

public enum CommandStatus
{
    Pending = 1,
    Sent = 2,
    Completed = 3,
    Failed = 4,
    Timeout = 5,
    Cancelled = 6
}