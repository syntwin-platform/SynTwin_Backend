namespace Syntwin.Application.Users.Dtos;

public sealed class CurrentSubscriptionResponse
{
    public string PlanCode { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public int MaxRobots { get; set; }
    public bool CanView3D { get; set; }
    public bool CanSendCommand { get; set; }
    public DateTimeOffset StartsAt { get; set; }
}