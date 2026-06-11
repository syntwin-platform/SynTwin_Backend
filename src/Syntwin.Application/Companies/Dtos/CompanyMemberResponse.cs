namespace Syntwin.Application.Companies.Dtos;

public sealed class CompanyMemberResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? AvatarUrl { get; set; }

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset JoinedAt { get; set; }
}
