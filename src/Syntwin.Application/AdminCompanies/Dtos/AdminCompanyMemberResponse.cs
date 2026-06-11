namespace Syntwin.Application.AdminCompanies.Dtos;

public sealed class AdminCompanyMemberResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public string Role { get; set; } = string.Empty;

    public DateTimeOffset JoinedAt { get; set; }
}
