namespace Syntwin.Application.Companies.Dtos;

public sealed class CompanyResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Industry { get; set; }

    public string? Address { get; set; }

    public string Timezone { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CurrentUserRole { get; set; } = string.Empty;

    public int MemberCount { get; set; }

    public string SubscriptionPlan { get; set; } = string.Empty;

    public int MaxRobots { get; set; }

    public bool CanView3D { get; set; }

    public bool CanSendCommand { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}