namespace Syntwin.Application.SubscriptionPlans.Dtos;

public sealed class SubscriptionPlanResponse
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal MonthlyPrice { get; set; }

    public int MaxRobots { get; set; }

    public bool CanView3D { get; set; }

    public bool CanSendCommand { get; set; }

    public int? AuditRetentionDays { get; set; }
}
