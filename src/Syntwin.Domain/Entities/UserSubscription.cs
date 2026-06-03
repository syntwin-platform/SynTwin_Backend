using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class UserSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public int PlanId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    public DateTimeOffset StartsAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndsAt { get; set; }

    public DateTimeOffset? CanceledAt { get; set; }

    public bool AutoRenew { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public User? User { get; set; }

    public SubscriptionPlan? Plan { get; set; }

    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
}