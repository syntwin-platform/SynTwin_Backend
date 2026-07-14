namespace Syntwin.Domain.Enums;

public enum FactoryRunTargetTerminationReason
{
    CommandFailure = 1,
    Collision = 2,
    ConnectionLost = 3,
    Timeout = 4,
    SafetyPolicy = 5,
    GroupPolicy = 6,
    UserCancelled = 7
}