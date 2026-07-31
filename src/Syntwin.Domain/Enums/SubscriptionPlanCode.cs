namespace Syntwin.Domain.Enums;

public enum SubscriptionPlanCode
{
    Free = 1,
    Starter = 2,
    Business = 3,
    Enterprise = 4,

    // Backward-compatible aliases for existing clients and old records.
    Basic = Starter,
    Premium = Business
}
