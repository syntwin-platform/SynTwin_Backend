namespace Syntwin.Application.Admin.Dtos;

public sealed class AdminUserDetailResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public string Timezone { get; set; } = "UTC";

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string SubscriptionPlan { get; set; } = string.Empty;

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}