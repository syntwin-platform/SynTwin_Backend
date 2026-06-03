namespace Syntwin.Application.Auth.Dtos;

public sealed class CurrentUserResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SubscriptionPlan { get; set; } = string.Empty;

    public bool CanView3D { get; set; }

    public bool CanSendCommand { get; set; }

    public int MaxRobots { get; set; }

    public string Timezone { get; set; } = "UTC";

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }
}