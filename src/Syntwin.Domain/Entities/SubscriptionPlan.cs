using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class SubscriptionPlan
{
    public int Id { get; set; }

    public SubscriptionPlanCode Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal MonthlyPrice { get; set; }

    public int MaxRobots { get; set; }

    public bool CanView3D { get; set; }

    public bool CanSendCommand { get; set; }

    public int? AuditRetentionDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
}