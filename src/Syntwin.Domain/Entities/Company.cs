using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Industry { get; set; }

    public string? Address { get; set; }

    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";

    public string? LogoUrl { get; set; }

    public CompanyStatus Status { get; set; } = CompanyStatus.Active;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<CompanyMember> Members { get; set; } = new List<CompanyMember>();

    public ICollection<Robot> Robots { get; set; } = new List<Robot>();
}
