namespace Syntwin.Application.Users.Dtos;

public sealed class CurrentUserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = string.Empty;
    public string Timezone { get; set; } = "UTC";
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
}