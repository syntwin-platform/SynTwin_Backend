using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class CompanyMember
{
    public Guid CompanyId { get; set; }

    public Guid UserId { get; set; }

    public CompanyMemberRole Role { get; set; } = CompanyMemberRole.Monitor;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Company? Company { get; set; }

    public User? User { get; set; }
}
