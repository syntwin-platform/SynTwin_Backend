using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class RobotSafetyPolicy
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? RobotId { get; set; }

    public RobotSafetyPolicyScope Scope { get; set; }

    public string Name { get; set; } = string.Empty;

    public string RobotModel { get; set; } = string.Empty;

    public string PolicyJson { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public Guid CreatedByUserId { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Company? Company { get; set; }

    public Robot? Robot { get; set; }

    public User? CreatedByUser { get; set; }

    public User? UpdatedByUser { get; set; }
}